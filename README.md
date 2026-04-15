# IoTSharp AtomUI Client

一个基于 **Avalonia + AtomUI** 的 IoTSharp 桌面客户端示例，替换了仓库中原有的 MAUI 项目。

## 已支持功能

- 登录 IoTSharp 服务
- 处理 IoTSharp 登录滑块验证码
- 获取当前用户 / 客户 / 租户上下文
- 按客户加载设备列表并支持名称筛选
- 查看设备详情和最新属性
- 查看设备最新遥测数据
- 按时间范围、keys、every、aggregate 查询聚合遥测数据

## 项目结构

- `IoTSharp.Client.slnx`：解决方案文件
- `IoTSharp.Client/`：Avalonia + AtomUI 桌面客户端项目

## 本地运行

```bash
dotnet build /home/runner/work/Client/Client/IoTSharp.Client.slnx
dotnet run --project /home/runner/work/Client/Client/IoTSharp.Client/IoTSharp.Client.csproj
```

## 使用说明

1. 输入 IoTSharp 服务地址，例如 `http://localhost:5000`
2. 输入用户名和密码
3. 拖动验证码拼图块到缺口位置后登录
4. 登录成功后，在左侧选择设备
5. 在右侧查看属性、最新遥测和聚合查询结果

## 技术说明

- UI：Avalonia + AtomUI
- 状态管理：CommunityToolkit.Mvvm
- 数据访问：`HttpClient` 调用 IoTSharp REST API
