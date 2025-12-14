# FluentZip

<div align="center">

![FluentZip](Assets/Square150x150Logo.scale-200.png)

一个采用现代 Fluent Design 设计的 Windows 压缩文件管理工具

A modern archive manager for Windows with Fluent Design

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![Windows](https://img.shields.io/badge/Windows-10%2B-blue.svg)](https://www.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

</div>

---

## 📖 简介 | Introduction

**中文**

FluentZip 是一款基于 WinUI 3 开发的现代化压缩文件管理工具，采用 Windows 11 Fluent Design 设计语言，为用户提供美观、流畅的压缩文件管理体验。

**English**

FluentZip is a modern archive manager built with WinUI 3, featuring Windows 11 Fluent Design for a beautiful and smooth file compression experience.

---

## ✨ 功能特性 | Features

**中文**

- 🗜️ **多格式支持**: 支持 ZIP、7Z、RAR 等常见压缩格式
- 🎨 **现代化界面**: 采用 Fluent Design 设计，支持 Mica 材质效果
- 📁 **文件浏览**: 树形目录视图，方便浏览压缩包内容
- 👁️ **文件预览**: 内置图片预览功能
- 🔍 **搜索功能**: 快速搜索压缩包内的文件
- 📝 **文件操作**: 支持添加、删除、重命名文件
- 💾 **解压缩**: 灵活的解压选项
- 📋 **最近文件**: 记录最近打开的压缩包
- 🌓 **主题支持**: 支持浅色/深色主题
- 🔐 **编码支持**: 支持多种文本编码

**English**

- 🗜️ **Multi-format Support**: Supports ZIP, 7Z, RAR and other common formats
- 🎨 **Modern UI**: Fluent Design with Mica material effects
- 📁 **File Browser**: Tree view for easy navigation
- 👁️ **File Preview**: Built-in image preview
- 🔍 **Search**: Quick file search within archives
- 📝 **File Operations**: Add, delete, and rename files
- 💾 **Extract**: Flexible extraction options
- 📋 **Recent Files**: Track recently opened archives
- 🌓 **Theme Support**: Light/Dark theme support
- 🔐 **Encoding Support**: Multiple text encoding support

---

## 💻 系统要求 | System Requirements

**中文**

- Windows 10 版本 1809 (17763) 或更高
- .NET 8.0 运行时
- 支持架构: x64, x86, ARM64

**English**

- Windows 10 version 1809 (17763) or higher
- .NET 8.0 Runtime
- Supported architectures: x64, x86, ARM64

---

## 🚀 安装使用 | Installation

**中文**

### 从源码构建

1. 确保已安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 确保已安装 [Windows App SDK](https://docs.microsoft.com/windows/apps/windows-app-sdk/)
3. 克隆仓库
   ```bash
   git clone https://github.com/SUlTlUS/FluentZip.git
   cd FluentZip
   ```
4. 使用 Visual Studio 2022 打开 `FluentZip.slnx`
5. 选择目标平台 (x64/x86/ARM64)
6. 按 F5 运行或构建解决方案

### 直接运行

构建完成后，可执行文件位于 `bin` 目录下。

**English**

### Build from Source

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install [Windows App SDK](https://docs.microsoft.com/windows/apps/windows-app-sdk/)
3. Clone the repository
   ```bash
   git clone https://github.com/SUlTlUS/FluentZip.git
   cd FluentZip
   ```
4. Open `FluentZip.slnx` with Visual Studio 2022
5. Select target platform (x64/x86/ARM64)
6. Press F5 to run or build the solution

### Run

After building, the executable can be found in the `bin` directory.

---

## 📚 使用指南 | Usage Guide

**中文**

### 打开压缩包

1. 点击主页的"打开压缩包"按钮
2. 选择要打开的压缩文件
3. 在归档视图中浏览和管理文件

### 创建压缩包

1. 点击主页的"新建压缩包"按钮
2. 选择要压缩的文件或文件夹
3. 配置压缩选项（格式、压缩级别等）
4. 点击"创建"完成

### 解压文件

1. 在归档视图中选择文件
2. 点击"解压"按钮
3. 选择解压目标位置
4. 等待解压完成

**English**

### Open Archive

1. Click "Open Archive" on the home page
2. Select the archive file to open
3. Browse and manage files in the archive view

### Create Archive

1. Click "New Archive" on the home page
2. Select files or folders to compress
3. Configure compression options (format, level, etc.)
4. Click "Create" to finish

### Extract Files

1. Select files in the archive view
2. Click the "Extract" button
3. Choose the extraction destination
4. Wait for extraction to complete

---

## 🛠️ 开发构建 | Development

**中文**

### 技术栈

- **框架**: .NET 8.0 + WinUI 3
- **UI 库**: Windows App SDK 1.8
- **压缩库**: 
  - 7-Zip (x64/ARM64)
  - SharpCompress 0.42.0
- **UI 组件**: 
  - CommunityToolkit.WinUI
  - System.Drawing.Common

### 项目结构

```
FluentZip/
├── Assets/              # 资源文件（图标、7-Zip 可执行文件）
├── Services/            # 服务层代码
├── *.xaml               # UI 界面定义
├── *.xaml.cs           # UI 逻辑代码
├── FluentZip.csproj    # 项目文件
└── README.md           # 本文件
```

### 构建命令

```bash
# 调试构建
dotnet build

# 发布构建 (x64)
dotnet publish -c Release -r win-x64

# 发布构建 (ARM64)
dotnet publish -c Release -r win-arm64
```

**English**

### Tech Stack

- **Framework**: .NET 8.0 + WinUI 3
- **UI Library**: Windows App SDK 1.8
- **Compression Libraries**: 
  - 7-Zip (x64/ARM64)
  - SharpCompress 0.42.0
- **UI Components**: 
  - CommunityToolkit.WinUI
  - System.Drawing.Common

### Project Structure

```
FluentZip/
├── Assets/              # Resources (icons, 7-Zip executables)
├── Services/            # Service layer code
├── *.xaml               # UI definitions
├── *.xaml.cs           # UI logic code
├── FluentZip.csproj    # Project file
└── README.md           # This file
```

### Build Commands

```bash
# Debug build
dotnet build

# Release build (x64)
dotnet publish -c Release -r win-x64

# Release build (ARM64)
dotnet publish -c Release -r win-arm64
```

---

## 📄 许可证 | License

**中文**

本项目使用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

**English**

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## 🙏 致谢 | Credits

**中文**

- [7-Zip](https://www.7-zip.org/) - 强大的压缩工具
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) - .NET 压缩库
- [Windows Community Toolkit](https://github.com/CommunityToolkit/WindowsCommunityToolkit) - UI 组件库
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - WinUI 3 框架

**English**

- [7-Zip](https://www.7-zip.org/) - Powerful compression tool
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) - .NET compression library
- [Windows Community Toolkit](https://github.com/CommunityToolkit/WindowsCommunityToolkit) - UI component library
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - WinUI 3 framework

---

## 📞 联系方式 | Contact

**中文**

如有问题或建议，欢迎提交 [Issue](https://github.com/SUlTlUS/FluentZip/issues)。

**English**

For questions or suggestions, please submit an [Issue](https://github.com/SUlTlUS/FluentZip/issues).

---

<div align="center">

**Made with ❤️ using WinUI 3**

</div>