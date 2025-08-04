using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.CompilerServices;

namespace WFuzzAgent
{
    /// <summary>
    /// 覆盖率收集器 - 与AFL/SharpFuzz兼容的共享内存覆盖率跟踪
    /// </summary>
    public static class CoverageCollector
    {
        // AFL标准共享内存大小: 64KB
        private const int MAP_SIZE = 65536;
        
        // 共享内存句柄
        private static IntPtr _sharedMemory = IntPtr.Zero;
        private static byte[] _coverageMap;
        private static readonly object _lock = new object();
        
        // 前一个位置，用于边缘覆盖率计算
        [ThreadStatic]
        private static ushort _prevLocation;
        
        /// <summary>
        /// 初始化覆盖率收集器
        /// </summary>
        /// <param name="sharedMemoryId">共享内存ID（来自环境变量__AFL_SHM_ID）</param>
        public static void Initialize(string sharedMemoryId = null)
        {
            if (_sharedMemory != IntPtr.Zero)
                return;
                
            lock (_lock)
            {
                if (_sharedMemory != IntPtr.Zero)
                    return;
                    
                // 尝试从环境变量获取共享内存ID
                if (string.IsNullOrEmpty(sharedMemoryId))
                {
                    sharedMemoryId = Environment.GetEnvironmentVariable("__AFL_SHM_ID");
                }
                
                if (!string.IsNullOrEmpty(sharedMemoryId))
                {
                    // AFL模式 - 连接到现有共享内存
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        InitializeWindows(sharedMemoryId);
                    }
                    else
                    {
                        InitializeUnix(sharedMemoryId);
                    }
                }
                else
                {
                    // 独立模式 - 使用本地内存
                    _coverageMap = new byte[MAP_SIZE];
                    _sharedMemory = Marshal.AllocHGlobal(MAP_SIZE);
                    Marshal.Copy(_coverageMap, 0, _sharedMemory, MAP_SIZE);
                }
            }
        }
        
        /// <summary>
        /// 记录代码块执行（AFL兼容的边缘覆盖率）
        /// </summary>
        /// <param name="location">当前位置ID</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordBlock(ushort location)
        {
            if (_sharedMemory == IntPtr.Zero)
                return;
                
            // AFL边缘覆盖率算法
            ushort index = (ushort)(location ^ _prevLocation);
            
            unsafe
            {
                byte* map = (byte*)_sharedMemory.ToPointer();
                map[index]++;
            }
            
            _prevLocation = (ushort)(location >> 1);
        }
        
        /// <summary>
        /// 重置覆盖率数据
        /// </summary>
        public static void Reset()
        {
            if (_sharedMemory == IntPtr.Zero)
                return;
                
            unsafe
            {
                byte* map = (byte*)_sharedMemory.ToPointer();
                for (int i = 0; i < MAP_SIZE; i++)
                {
                    map[i] = 0;
                }
            }
            
            _prevLocation = 0;
        }
        
        /// <summary>
        /// 获取当前覆盖率数据的副本
        /// </summary>
        public static byte[] GetCoverageData()
        {
            if (_sharedMemory == IntPtr.Zero)
                return null;
                
            byte[] data = new byte[MAP_SIZE];
            Marshal.Copy(_sharedMemory, data, 0, MAP_SIZE);
            return data;
        }
        
        /// <summary>
        /// 计算覆盖率统计信息
        /// </summary>
        public static CoverageStatistics GetStatistics()
        {
            var stats = new CoverageStatistics();
            
            if (_sharedMemory == IntPtr.Zero)
                return stats;
                
            unsafe
            {
                byte* map = (byte*)_sharedMemory.ToPointer();
                for (int i = 0; i < MAP_SIZE; i++)
                {
                    if (map[i] > 0)
                    {
                        stats.CoveredEdges++;
                        stats.TotalHits += map[i];
                    }
                }
            }
            
            stats.CoveragePercentage = (stats.CoveredEdges * 100.0) / MAP_SIZE;
            return stats;
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            lock (_lock)
            {
                if (_sharedMemory != IntPtr.Zero && _coverageMap != null)
                {
                    // 本地模式清理
                    Marshal.FreeHGlobal(_sharedMemory);
                    _coverageMap = null;
                }
                else if (_sharedMemory != IntPtr.Zero)
                {
                    // 共享内存模式清理
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        CleanupWindows();
                    }
                    else
                    {
                        CleanupUnix();
                    }
                }
                
                _sharedMemory = IntPtr.Zero;
            }
        }
        
        #region 平台特定实现
        
        // Windows共享内存实现
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, 
            uint dwFileOffsetHigh, uint dwFileOffsetLow, IntPtr dwNumberOfBytesToMap);
            
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        
        private const uint FILE_MAP_ALL_ACCESS = 0xF001F;
        private static IntPtr _windowsHandle;
        
        private static void InitializeWindows(string shmId)
        {
            _windowsHandle = OpenFileMapping(FILE_MAP_ALL_ACCESS, false, $"afl_shm_{shmId}");
            if (_windowsHandle == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to open shared memory: {Marshal.GetLastWin32Error()}");
                
            _sharedMemory = MapViewOfFile(_windowsHandle, FILE_MAP_ALL_ACCESS, 0, 0, new IntPtr(MAP_SIZE));
            if (_sharedMemory == IntPtr.Zero)
            {
                CloseHandle(_windowsHandle);
                throw new InvalidOperationException($"Failed to map shared memory: {Marshal.GetLastWin32Error()}");
            }
        }
        
        private static void CleanupWindows()
        {
            if (_sharedMemory != IntPtr.Zero)
            {
                UnmapViewOfFile(_sharedMemory);
                _sharedMemory = IntPtr.Zero;
            }
            
            if (_windowsHandle != IntPtr.Zero)
            {
                CloseHandle(_windowsHandle);
                _windowsHandle = IntPtr.Zero;
            }
        }
        
        // Unix/Linux共享内存实现
        [DllImport("libc", SetLastError = true)]
        private static extern IntPtr shmat(int shmid, IntPtr shmaddr, int shmflg);
        
        [DllImport("libc", SetLastError = true)]
        private static extern int shmdt(IntPtr shmaddr);
        
        private static void InitializeUnix(string shmId)
        {
            if (!int.TryParse(shmId, out int id))
                throw new ArgumentException($"Invalid shared memory ID: {shmId}");
                
            _sharedMemory = shmat(id, IntPtr.Zero, 0);
            if (_sharedMemory == new IntPtr(-1))
                throw new InvalidOperationException($"Failed to attach shared memory: {Marshal.GetLastPInvokeError()}");
        }
        
        private static void CleanupUnix()
        {
            if (_sharedMemory != IntPtr.Zero && _sharedMemory != new IntPtr(-1))
            {
                shmdt(_sharedMemory);
                _sharedMemory = IntPtr.Zero;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 覆盖率统计信息
    /// </summary>
    public class CoverageStatistics
    {
        public int CoveredEdges { get; set; }
        public long TotalHits { get; set; }
        public double CoveragePercentage { get; set; }
        
        public override string ToString()
        {
            return $"Edges: {CoveredEdges}, Hits: {TotalHits}, Coverage: {CoveragePercentage:F2}%";
        }
    }
}