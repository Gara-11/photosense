# PhotoSense 1.0.15 源码说明

这是 PhotoSense 的可重新构建源码包。源码包不包含 API Key、Windows 当前用户配置、运行日志、测试截图、编译缓存或历史发布文件。

## 目录结构

- `src/`：WinForms 应用源码，包括 Photoshop 连接、局部蒙版画布、图像接口、UI Scale 和 Real-ESRGAN 集成。
- `assets/`：应用内嵌的品牌图片资源。
- `PixelPatchStudio.csproj`：Visual Studio / MSBuild 项目文件。
- `app.manifest`：Windows Per-Monitor V2 DPI 配置。
- `build.ps1`：一键编译和自检脚本。
- `download-realesrgan.ps1`：Real-ESRGAN 自动部署备用脚本。
- `README.md`：软件使用与接口配置说明。

## 构建

系统要求：Windows 10/11、.NET Framework 4.8。

在源码根目录打开 PowerShell，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

脚本会先生成测试程序并运行自检，然后把正式程序输出到：

```text
outputs\PhotoSense.exe
```

程序不依赖 Python、Node.js 或 NuGet 才能启动。Real-ESRGAN 是可选组件，可在软件内自动部署。

## 用户数据

API Key、接口设置、模型和 UI Scale 不保存在源码目录，而是固定保存在：

```text
%LOCALAPPDATA%\PixelPatch Studio
```

因此更换源码目录、重新编译或替换 EXE 不会清除当前 Windows 用户已经保存的配置。
