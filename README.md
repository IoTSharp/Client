# IoTSharp .NET MAUI Client

一个基于 **.NET MAUI** 的 IoTSharp 客户端示例，用于替代依赖 **Avalonia + AtomUI** 商业授权的实现。

## 已支持功能

- 登录 IoTSharp 服务
- 处理 IoTSharp 登录滑块验证码
- 获取当前用户 / 客户 / 租户上下文
- 按客户加载设备列表并支持名称筛选
- 查看设备详情和最新属性
- 查看设备最新遥测数据
- 按时间范围、keys、every、aggregate 查询聚合遥测数据
- 快速切换最近 1 小时 / 24 小时 / 7 天查询窗口
- 以图形方式预览最新数值型遥测快照和聚合趋势

## 项目结构

- `IoTSharp.Client.slnx`：解决方案文件
- `IoTSharp.Client/`：.NET MAUI 客户端项目

## 本地运行

```bash
dotnet build /home/runner/work/Client/Client/IoTSharp.Client.slnx
# Android
dotnet build -t:Run -f net9.0-android /home/runner/work/Client/Client/IoTSharp.Client/IoTSharp.Client.csproj
# Windows
dotnet build -t:Run -f net9.0-windows10.0.19041.0 /home/runner/work/Client/Client/IoTSharp.Client/IoTSharp.Client.csproj
```

## 使用说明

1. 输入 IoTSharp 服务地址，例如 `http://localhost:5000`
2. 输入用户名和密码
3. 拖动验证码拼图块到缺口位置后登录
4. 登录成功后，在左侧选择设备
5. 在右侧查看属性、最新遥测和聚合查询结果
6. 使用快捷时间按钮快速切换查询范围，并查看图形化趋势预览

## 技术说明

- UI：.NET MAUI
- 状态管理：CommunityToolkit.Mvvm
- 数据访问：`HttpClient` 调用 IoTSharp REST API
