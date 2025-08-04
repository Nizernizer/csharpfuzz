# 使用 .NET 9 SDK 作为构建阶段
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 复制解决方案文件
COPY *.sln ./

# 复制所有项目文件（保持目录结构）
COPY WFuzz/*.csproj ./WFuzz/
COPY WFuzzGen/*.csproj ./WFuzzGen/
COPY WFuzzEngine/*.csproj ./WFuzzEngine/
COPY WFuzzAgent/*.csproj ./WFuzzAgent/
COPY WFuzzCLI/*.csproj ./WFuzzCLI/
COPY WFuzzDriver/*.csproj ./WFuzzDriver/
COPY WFuzzTest/*.csproj ./WFuzzTest/
COPY TestLibrary/*.csproj ./TestLibrary/

# 还原所有项目的依赖
RUN dotnet restore

# 复制所有源代码
COPY . .

# 构建整个解决方案
RUN dotnet build -c Release --no-restore

# 发布主要的可执行项目
RUN dotnet publish WFuzzCLI/WFuzzCLI.csproj -c Release -o /app/cli --no-restore
RUN dotnet publish WFuzzGen/WFuzzGen.csproj -c Release -o /app/gen --no-restore
RUN dotnet publish WFuzzDriver/WFuzzDriver.csproj -c Release -o /app/driver --no-restore

# 构建测试库
RUN dotnet publish TestLibrary/TestLibrary.csproj -c Release -o /app/testlib --no-restore

# 使用 .NET 9 运行时作为最终镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# 安装必要的系统依赖
RUN apt-get update && apt-get install -y \
    # AFL++ 依赖
    build-essential \
    python3 \
    python3-pip \
    git \
    cmake \
    # 其他有用的工具
    curl \
    vim \
    && rm -rf /var/lib/apt/lists/*

# 安装 AFL++ (可选，如果需要 AFL 模式)
RUN git clone https://github.com/AFLplusplus/AFLplusplus.git /opt/aflplusplus && \
    cd /opt/aflplusplus && \
    make distrib && \
    make install && \
    cd / && \
    rm -rf /opt/aflplusplus

# 设置工作目录
WORKDIR /wfuzz

# 从构建阶段复制发布的文件
COPY --from=build /app/cli ./cli
COPY --from=build /app/gen ./gen
COPY --from=build /app/driver ./driver
COPY --from=build /app/testlib ./testlib

# 复制 PowerShell 测试脚本
COPY TestRunner.ps1 ./

# 创建必要的目录
RUN mkdir -p /wfuzz/output/seeds /wfuzz/output/findings /wfuzz/output/crashes

# 创建入口脚本
RUN echo '#!/bin/bash\n\
echo "=== CSharpFuzz Docker Container ==="\n\
echo "Available commands:"\n\
echo "  wfuzz-gen    - Generate test harnesses"\n\
echo "  wfuzz-cli    - Run fuzzing with CLI"\n\
echo "  wfuzz-driver - Run fuzzing with driver"\n\
echo "  wfuzz-test   - Run test script (requires PowerShell)"\n\
echo ""\n\
echo "Example usage:"\n\
echo "  # Generate test code:"\n\
echo "  wfuzz-gen /wfuzz/testlib/TestLibrary.dll /wfuzz/output/generated"\n\
echo ""\n\
echo "  # Run fuzzing:"\n\
echo "  wfuzz-cli run --assembly /wfuzz/output/generated/WFuzzGen.Generated.dll \\"\n\
echo "            --caller TestLibrary_Calculator_Divide_double_double \\"\n\
echo "            --engine SharpFuzz --input-dir /wfuzz/output/seeds"\n\
echo ""\n\
exec "$@"' > /usr/local/bin/docker-entrypoint.sh && \
    chmod +x /usr/local/bin/docker-entrypoint.sh

# 创建命令别名
RUN ln -s /wfuzz/gen/WFuzzGen /usr/local/bin/wfuzz-gen && \
    ln -s /wfuzz/cli/WFuzzCLI /usr/local/bin/wfuzz-cli && \
    ln -s /wfuzz/driver/WFuzzDriver /usr/local/bin/wfuzz-driver

# 设置环境变量
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV PATH="/usr/local/bin:${PATH}"

# 如果需要 PowerShell 来运行测试脚本
# RUN apt-get update && apt-get install -y wget && \
#     wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb && \
#     dpkg -i packages-microsoft-prod.deb && \
#     apt-get update && \
#     apt-get install -y powershell && \
#     rm packages-microsoft-prod.deb && \
#     rm -rf /var/lib/apt/lists/*

# 暴露端口（如果将来需要 Web 界面）
# EXPOSE 5000

# 设置卷，用于输入/输出数据
VOLUME ["/wfuzz/input", "/wfuzz/output", "/wfuzz/targets"]

# 设置入口点
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["bash"]