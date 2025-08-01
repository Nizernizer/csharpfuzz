using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestLibrary
{
    /// <summary>
    /// 基础计算类
    /// </summary>
    public class Calculator
    {
        public int Add(int a, int b) => a + b;
        
        public double Divide(double a, double b)
        {
            if (b == 0) throw new DivideByZeroException();
            return a / b;
        }
        
        public static int Multiply(int a, int b) => a * b;
        
        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Delay(10);
            return a + b;
        }
    }
    
    /// <summary>
    /// 字符串处理类
    /// </summary>
    public class StringProcessor
    {
        private string _prefix = "";
        
        public string Prefix
        {
            get => _prefix;
            set => _prefix = value ?? "";
        }
        
        public string Process(string input)
        {
            return _prefix + input;
        }
        
        public static string Reverse(string input)
        {
            if (input == null) return null!;
            var chars = input.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
        
        public string this[int index]
        {
            get => index < _prefix.Length ? _prefix[index].ToString() : "";
            set
            {
                if (index == 0 && !string.IsNullOrEmpty(value))
                {
                    _prefix = value[0] + _prefix.Substring(1);
                }
            }
        }
    }
    
    /// <summary>
    /// 泛型容器类
    /// </summary>
    public class Container<T>
    {
        private readonly List<T> _items = new List<T>();
        
        public int Count => _items.Count;
        
        public void Add(T item) => _items.Add(item);
        
        public T Get(int index) => _items[index];
        
        public T this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }
        
        public bool Contains(T item) => _items.Contains(item);
    }
    
    /// <summary>
    /// 可释放资源类
    /// </summary>
    public class ResourceManager : IDisposable
    {
        private bool _disposed = false;
        private readonly List<string> _resources = new List<string>();
        
        public void AddResource(string resource)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ResourceManager));
            _resources.Add(resource);
        }
        
        public int ResourceCount => _resources.Count;
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _resources.Clear();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// 事件处理类
    /// </summary>
    public class EventPublisher
    {
        public event EventHandler<string>? MessageReceived;
        
        public void SendMessage(string message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
    
    /// <summary>
    /// 操作符重载示例
    /// </summary>
    public class Vector2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        
        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }
        
        public static Vector2D operator +(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X + b.X, a.Y + b.Y);
        }
        
        public static Vector2D operator -(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X - b.X, a.Y - b.Y);
        }
        
        public static bool operator ==(Vector2D a, Vector2D b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.X == b.X && a.Y == b.Y;
        }
        
        public static bool operator !=(Vector2D a, Vector2D b)
        {
            return !(a == b);
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Vector2D other && this == other;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }
}