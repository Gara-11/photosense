# PhotoSense

> **AI should become part of the Photoshop workflow — not replace it.**  
> **让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。**

PhotoSense is an open-source AI-powered image editing and generative retouching tool designed for professional **Adobe Photoshop** workflows.

PhotoSense 是一款面向 **Adobe Photoshop 专业后期工作流** 开发的开源 AI 图像编辑与生成式重绘工具。

它将 Photoshop 的专业图像处理能力与现代生成式 AI 图像模型结合，使摄影师、修图师、设计师、数字艺术创作者能够直接在现有 Photoshop 工作流中进行：

- AI 局部重绘
- Generative Inpainting
- 内容替换
- 局部细节生成
- AI 辅助修图
- 蒙版约束生成
- 高分辨率 AI 工作流

PhotoSense 的核心目标并不是让 AI 替代 Photoshop。

而是让 AI 成为 Photoshop 中的一种新的创作工具。

---

> [!IMPORTANT]
> ## Official Project Information / 官方项目信息
>
> **Original Creator / 原作者:** `Lacrimosa1337`
>
> **Original Creator's Bilibili / 原作者 Bilibili:**  
> https://space.bilibili.com/68375663
>
> **GitHub Publisher & Repository Maintainer / GitHub 代发布与仓库维护:** `Gara-11`
>
> **Official Repository / 官方 GitHub 仓库:**  
> https://github.com/Gara-11/photosense
>
> Please beware of unofficial rebranded, repackaged, impersonated, closed-source, or misleading paid versions.
>
> **请警惕冒充官方、隐瞒原作者、删除开源声明、违规闭源、简单套皮或通过误导性宣传收费的第三方版本。**

---

# ✨ Features / 主要功能

## 🎨 Adobe Photoshop Workflow Integration

PhotoSense is designed to work **with Photoshop**, not replace it.

It can integrate AI generation directly into an existing professional Photoshop workflow.

主要工作流程包括：

```text
Adobe Photoshop
        ↓
读取当前图像
        ↓
创建人物 / 主体 / 背景选区
        ↓
手动精修蒙版
        ↓
GPT Image 2 / Nano Banana
        ↓
AI 局部生成
        ↓
Real-ESRGAN 超分（可选）
        ↓
本地蒙版合成
        ↓
透明补丁
        ↓
返回 Photoshop
        ↓
继续专业后期
```

传统 AI 修图通常需要：

```text
Photoshop
→ 导出图片
→ 上传 AI 平台
→ 输入 Prompt
→ 等待生成
→ 下载图片
→ 重新导入 Photoshop
```

PhotoSense 希望将其简化为：

```text
Photoshop
→ PhotoSense
→ AI Editing
→ Photoshop
```

从而减少大量重复导出、上传、下载和重新导入操作。

---

# 🤖 GPT Image 2 Support

PhotoSense supports **GPT Image 2-compatible image editing APIs**.

可用于：

- Mask-based Generative Editing
- Inpainting
- 局部重绘
- 内容替换
- 细节补全
- Prompt 驱动的图像编辑
- 指定区域重新生成
- AI 辅助修图

用户可以根据实际 API 服务配置：

```text
API Key
Base URL
Endpoint
Model
```

PhotoSense 并不要求 API 参数被硬编码在软件源代码中。

---

# 🍌 Nano Banana Support

PhotoSense supports **Nano Banana / Gemini-compatible image generation APIs**.

支持配置：

```text
API Base URL
API Endpoint
API Key
Model
Compatible Relay Service
```

这意味着 PhotoSense 可以根据 API 实现连接不同的兼容服务，而不必永久绑定单一服务商。

支持的使用方向包括：

- 局部图像重绘
- Prompt 图像修改
- 内容生成
- 细节增强
- 图像修复
- 蒙版约束生成

---

# 🎯 Automatic Selection / 自动选区

PhotoSense 可以结合 Photoshop 的选区能力快速建立 AI 编辑区域。

支持的目标类型包括：

- Person / 人物
- Subject / 主体
- Background / 背景

自动选区完成后，用户仍然可以进一步手动修改蒙版。

---

# 🖌️ Mask Editor / 蒙版编辑

PhotoSense 提供独立的蒙版编辑工作流，让用户精确决定：

> **AI 到底允许修改哪里。**

支持：

- Brush / 画笔添加蒙版
- Eraser / 橡皮擦除蒙版
- Brush Size / 调整画笔尺寸
- Zoom / 缩放
- Pan / 平移
- Undo / 撤销
- Redo / 重做
- 手动精修 AI 编辑区域

相比直接把整张图片交给 AI 重新生成，蒙版工作流可以提供更高的可控性。

---

# 🧩 Mask-Constrained Generative Editing

PhotoSense 的核心设计之一，是尽可能限制 AI 只修改用户指定的区域。

AI 生成结束后，可以通过本地蒙版重新进行图像合成：

```text
Inside Mask
→ AI Generated Result

Outside Mask
→ Original Image Pixels
```

中文：

```text
蒙版内部
→ 使用 AI 生成结果

蒙版外部
→ 保留原始图像像素
```

这样可以降低 AI 对以下区域产生意外修改的概率：

- 人物身份
- 面部
- 构图
- 背景
- 服装
- 色彩
- 不需要修改的细节

---

# 🔄 Non-Destructive Transparent Patch Workflow

PhotoSense 可以将 AI 编辑后的区域作为 **Transparent Patch / 透明补丁** 返回 Photoshop。

工作方式：

```text
Original Photoshop Document
        ↓
PhotoSense reads image
        ↓
Create / refine mask
        ↓
GPT Image 2 / Nano Banana
        ↓
AI generation
        ↓
Local mask compositing
        ↓
Transparent edited patch
        ↓
Return to Photoshop
```

透明补丁中：

```text
Edited Region
→ AI Result

Unedited Region
→ Transparent
```

这样不会简单地使用 AI 生成结果直接覆盖整个原图。

用户回到 Photoshop 后仍然可以继续：

- 调整图层透明度
- 添加 Layer Mask
- 使用 Blend Mode
- Camera Raw
- Frequency Separation
- Dodge & Burn
- Liquify
- 色彩调整
- 局部修图
- 其他 Photoshop 专业后期操作

这也是 PhotoSense 最重要的设计理念之一：

> **让 AI 成为 Photoshop 图层工作流的一部分。**

---

# 🔍 Real-ESRGAN Super-Resolution

PhotoSense 提供可选的 **Real-ESRGAN** 本地 AI 超分辨率支持。

AI 图像生成完成后，可以根据工作流程进一步执行超分处理，再进行最终图像合成。

项目中包含：

```text
download-realesrgan.ps1
```

用于准备相关 Real-ESRGAN 组件。

Real-ESRGAN 属于可选功能。

即使不安装 Real-ESRGAN，也不会影响 PhotoSense 基础 AI 重绘功能的核心逻辑。

---

# 🔐 API Credential Security

PhotoSense 不建议在源码中直接硬编码真实 API Key。

在支持的 Windows 环境中，API 凭据可以通过当前 Windows 用户级别的加密机制进行保护。

任何开发者在提交代码前，都应该确保以下敏感信息没有被上传：

```text
API Keys
Access Tokens
Passwords
Private Credentials
Private Endpoints
Secret Files
```

尤其不要提交：

```text
.env
secrets.json
credentials.json
api_keys.json
config.local.json
```

如果一个 API Key 曾经被错误上传到公开 GitHub 仓库，应立即撤销并重新生成。

---

# 🧠 Design Philosophy / 设计理念

PhotoSense 的核心理念是：

> **AI should become part of the Photoshop workflow — not replace it.**

> **让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。**

生成式 AI 非常强大。

但专业图像后期仍然需要精确控制：

- Composition / 构图
- Color / 色彩
- Layers / 图层
- Masks / 蒙版
- Identity Consistency / 人物一致性
- Local Adjustments / 局部调整
- Retouching / 精修
- Fine Details / 细节
- Final Artistic Control / 最终艺术控制

PhotoSense 不把 AI 当作一个独立的“一键生图工具”。

它更希望把 AI 变成：

```text
Photoshop Professional Workflow
+
Generative AI
```

中的一个新工具。

---

# 🚀 Workflow

```text
Adobe Photoshop
      │
      ▼
Import Active Image
      │
      ▼
Automatic Selection
Person / Subject / Background
      │
      ▼
Manual Mask Refinement
Brush / Eraser / Zoom / Pan
      │
      ▼
Select AI Backend
GPT Image 2 / Nano Banana
      │
      ▼
Prompt + Mask
      │
      ▼
AI Generation
      │
      ▼
Optional Real-ESRGAN Upscaling
      │
      ▼
Local Mask Compositing
      │
      ▼
Transparent Patch
      │
      ▼
Return to Photoshop
      │
      ▼
Continue Professional Retouching
```

---

# 🖥️ Requirements

PhotoSense currently targets Windows-based Photoshop workflows.

Recommended environment:

```text
Windows 10 / Windows 11
Adobe Photoshop
.NET Framework 4.8
```

AI backend requires at least one compatible service:

```text
GPT Image 2-compatible API

or

Nano Banana / Gemini-compatible API
```

Optional:

```text
Real-ESRGAN
```

for local AI super-resolution.

---

# 🛠️ Building from Source

Clone the repository:

```bash
git clone https://github.com/Gara-11/photosense.git
```

Enter the project directory:

```bash
cd photosense
```

The project is primarily written in:

```text
C#
.NET Framework 4.8
```

Current project file:

```text
PixelPatchStudio.csproj
```

> The internal project name may still contain historical development naming.  
> The public software/project name is **PhotoSense**.

项目内部目前仍可能存在早期开发阶段使用的旧项目命名。

软件正式公开名称为：

```text
PhotoSense
```

The repository also includes:

```text
build.ps1
```

for project build-related tasks.

Depending on your Windows PowerShell execution policy, local script execution may need to be enabled before running PowerShell scripts.

---

# 📁 Project Structure

```text
photosense/
│
├── assets/
│   └── Application resources
│
├── src/
│   └── PhotoSense source code
│
├── .gitignore
├── LICENSE
├── README.md
├── SOURCE_README.md
├── app.manifest
├── build.ps1
├── download-realesrgan.ps1
└── PixelPatchStudio.csproj
```

Build-generated files such as:

```text
bin/
obj/
outputs/
```

should not normally be committed to the source repository.

---

# 🔑 API Configuration

Depending on the selected AI provider, users may need to configure:

```text
API Key
Base URL
Endpoint
Model
```

PhotoSense may also support compatible third-party API relay services depending on the API implementation.

Third-party APIs are independent services.

Users are responsible for:

- Their own API credentials
- API costs
- API usage policies
- Model availability
- Service availability
- Third-party terms of service

---

# 👤 Creator & Maintainer / 创作者与维护者

## Original Creator / 原作者

### **Lacrimosa1337**

**Lacrimosa1337 is the original creator and primary author of PhotoSense.**

The original concept, software design, core implementation, and primary development of PhotoSense were created by **Lacrimosa1337**.

PhotoSense 的：

- 原始创意
- 软件设计
- 核心实现
- 主体程序
- 主要开发工作

均由 **Lacrimosa1337** 完成。

### Original Creator's Social Account / 原作者社交账号

**Bilibili / 哔哩哔哩**

https://space.bilibili.com/68375663

该 Bilibili 账号属于 PhotoSense 原作者：

```text
Lacrimosa1337
```

---

## GitHub Publisher & Repository Maintainer

### **Gara-11**

Gara-11 is responsible for publishing and maintaining the GitHub repository on behalf of the project.

Main responsibilities include:

- Publishing the source code repository
- Maintaining the GitHub repository
- Organizing releases
- Maintaining README and documentation
- Assisting with Issues and Pull Requests
- Assisting with project distribution
- Assisting with community and repository management

**Gara-11 is not the original creator of PhotoSense.**

中文：

**Gara-11 并非 PhotoSense 原作者。**

Gara-11 主要负责：

- 代为发布 GitHub 仓库
- GitHub 仓库维护
- 整理版本发布
- README 与文档维护
- Issues / Pull Requests 管理协助
- 软件分发协助
- 开源社区及项目维护

GitHub:

https://github.com/Gara-11

---

# 🌐 Official Project Links / 官方项目渠道

## Official GitHub Repository

https://github.com/Gara-11/photosense

## Original Creator

```text
Lacrimosa1337
```

## Original Creator's Bilibili

https://space.bilibili.com/68375663

## GitHub Repository Maintainer

```text
Gara-11
```

请优先通过以上渠道核实：

- 原作者身份
- 官方源码
- 项目公告
- Release
- 版本更新
- 项目声明

---

# 🚫 Rebranding, Impersonation & Misleading Distribution Notice

PhotoSense is distributed under the:

**GNU General Public License v3.0 (GPL-3.0)**

Open source does **not** mean that third parties may falsely claim authorship, hide the origin of the project, violate GPL obligations, or impersonate the official PhotoSense project.

The original creator of PhotoSense is:

```text
Lacrimosa1337
```

The official public GitHub repository is currently maintained by:

```text
Gara-11
```

Official repository:

```text
https://github.com/Gara-11/photosense
```

---

## The PhotoSense project explicitly opposes practices including:

- Removing or hiding the identity of the original creator
- Falsifying authorship
- Claiming PhotoSense or substantially derived code as completely independent original software
- Making superficial UI, logo, color, branding, or naming changes and hiding the project's true origin
- Rebranding or "reskinning" PhotoSense while misleading users into believing it is independently developed software
- Presenting an unofficial fork as an official PhotoSense release
- Impersonating the original project
- Falsely claiming official authorization
- Falsely claiming partnership or endorsement by the original creator
- Using PhotoSense branding to create misleading paid products
- Removing copyright or license information
- Removing required attribution
- Violating GPL-3.0 source-code obligations
- Distributing GPL-covered derivative works as proprietary closed-source software in violation of the GPL
- Refusing to provide corresponding source code where GPL-3.0 requires it
- Using information asymmetry to sell a superficially modified version while hiding the fact that it is derived from PhotoSense

Forks and derivative projects must not mislead users about their origin or official status.

---

# 🚫 关于套皮、冒充原作者及误导性收费的声明

PhotoSense 真正的原作者为：

```text
Lacrimosa1337
```

原作者 Bilibili：

```text
https://space.bilibili.com/68375663
```

GitHub 仓库目前由：

```text
Gara-11
```

代为发布并维护。

官方 GitHub 仓库：

```text
https://github.com/Gara-11/photosense
```

---

## 开源不等于可以冒充原创

PhotoSense 采用 GPL-3.0 开源。

但是：

> **开源不代表可以删除原作者。**

> **开源不代表可以把别人的项目换个 Logo 就谎称为独立原创。**

> **开源不代表可以违规闭源。**

> **开源不代表可以冒充官方版本。**

> **开源不代表可以通过虚假授权或误导宣传欺骗用户。**

PhotoSense 项目明确反对以下行为，包括但不限于：

- 删除、隐藏或篡改原作者 **Lacrimosa1337** 的身份
- 将 PhotoSense 或大量基于 PhotoSense 的代码谎称为完全独立原创软件
- 仅修改名称、Logo、配色、UI 或少量功能后进行简单“套皮”
- 套皮后故意隐瞒软件基于 PhotoSense 开发的事实
- 删除 PhotoSense 的来源信息后进行二次发布
- 冒充 PhotoSense 官方版本
- 冒充 PhotoSense 原作者
- 虚假声称获得 PhotoSense 官方授权
- 虚假声称与原作者存在合作关系
- 使用 PhotoSense 名称、Logo、截图、视觉资产或项目身份进行误导性宣传
- 删除 GPL 许可证
- 删除依法或 GPL 要求保留的版权与许可证信息
- 将 GPL-3.0 覆盖的衍生代码违规闭源
- 在 GPL 要求提供对应源代码时拒绝提供源码
- 将简单换皮或重新打包后的 PhotoSense 宣传为“完全自主研发”
- 利用用户不了解项目真实来源的信息差进行误导性收费

---

# ⚠️ About Paid Distribution / 关于收费分发

PhotoSense currently uses the **GPL-3.0** license.

GPL-3.0 allows charging money for software distribution or related services when all applicable GPL obligations are followed.

Therefore:

> **Charging money does not automatically violate GPL.**

However:

> **Charging money does not give anyone the right to hide the original creator.**

> **Charging money does not allow false claims of authorship.**

> **Charging money does not allow GPL-covered derivative works to be illegally closed-source.**

> **Charging money does not allow removal of required license notices.**

> **Charging money does not allow impersonation of the official PhotoSense project.**

> **Charging money does not allow misleading users into believing that an unofficial derivative is an officially authorized version.**

中文：

GPL-3.0 本身允许在遵守许可证要求的情况下收费分发软件或提供相关服务。

但是：

> **允许收费，不代表允许冒充原作者。**

> **允许收费，不代表允许隐瞒 PhotoSense 的真实来源。**

> **允许收费，不代表允许把 GPL 项目违规闭源。**

> **允许收费，不代表允许删除 GPL 许可证。**

> **允许收费，不代表允许删除依法需要保留的版权信息。**

> **允许收费，不代表可以简单套皮后谎称完全自主研发。**

> **允许收费，不代表可以冒充 PhotoSense 官方授权版本。**

---

# 🚨 Official Identity Notice / 官方身份声明

Unless explicitly announced by the original creator or through the official repository, third parties must not falsely claim that their version is:

```text
Official PhotoSense

PhotoSense Official Edition

PhotoSense Official Pro

PhotoSense Pro Official Edition

PhotoSense Premium Official Edition

PhotoSense Authorized Edition

PhotoSense Official Partner Edition

Officially Authorized by PhotoSense

Officially Authorized by Lacrimosa1337
```

除非原作者或官方 GitHub 仓库明确发布授权声明，否则任何第三方不得通过以下或类似名称误导用户：

```text
PhotoSense 官方版

PhotoSense 官方增强版

PhotoSense Pro 官方版

PhotoSense Premium 官方版

PhotoSense 官方授权版

PhotoSense 官方合作版

Lacrimosa1337 官方授权版
```

Fork 或衍生项目应当明确说明：

```text
This is an unofficial derivative project based on PhotoSense.

这是一个基于 PhotoSense 修改的非官方衍生项目。
```

不得通过名称、Logo、宣传页面、收费页面或其他方式让普通用户误认为其属于 PhotoSense 官方发布。

---

# 🤝 Contributing

Contributions are welcome.

You can contribute by:

- Reporting bugs
- Opening Issues
- Suggesting features
- Improving documentation
- Submitting Pull Requests
- Improving Photoshop integration
- Adding compatible AI backend support
- Improving mask workflows
- Improving high-resolution workflows
- Fixing bugs
- Improving performance

For major changes, opening an Issue first to discuss the proposal is recommended.

Contributors should not commit personal API credentials or private information.

---

# 🗺️ Roadmap

Possible future development directions include:

- Improved Photoshop integration
- Additional compatible generative image models
- Better API provider management
- Improved mask editing
- Better high-resolution workflows
- Preset management
- Better model management
- Simplified installation
- Automatic update support
- Performance optimization
- Better error handling
- Improved UI / UX
- Additional professional retouching workflows

The roadmap may change as the project develops.

---

# 📸 Screenshots

Screenshots, examples, and workflow demonstrations may be added in future releases.

---

# ⚠️ Disclaimer

PhotoSense is an independent open-source project.

Adobe Photoshop is a product and trademark of Adobe Inc.

OpenAI, GPT, and related products or services belong to their respective rights holders.

Gemini, Google, and related products or services belong to their respective rights holders.

Real-ESRGAN is a separate third-party open-source project maintained by its respective authors and contributors.

PhotoSense is not automatically affiliated with, endorsed by, sponsored by, or officially partnered with:

- Adobe
- OpenAI
- Google
- Real-ESRGAN developers
- Third-party API providers

unless explicitly stated otherwise.

Users are responsible for complying with:

- Third-party API terms
- Model usage policies
- Copyright laws
- Privacy laws
- Applicable local laws
- API billing requirements
- Third-party service rules

Use of a third-party API through PhotoSense does not imply endorsement of that provider by the PhotoSense project or its original creator.

---

# 📄 License

PhotoSense is licensed under the:

## GNU General Public License v3.0

You may:

- Use the software
- Study the source code
- Modify the software
- Fork the project
- Redistribute the software
- Distribute modified versions

subject to the requirements and obligations of GPL-3.0.

Derivative works covered by GPL-3.0 must follow the applicable GPL requirements.

See:

```text
LICENSE
```

for the complete license terms.

---

# ❤️ Support the Original Project

If PhotoSense is useful to you, please consider supporting the original project by:

- ⭐ Starring the official GitHub repository
- 🐛 Reporting bugs through GitHub Issues
- 🔧 Contributing improvements
- 🔀 Submitting Pull Requests
- 📢 Sharing the official repository
- 🚫 Avoiding misleading unofficial repackaged versions
- 📺 Following the original creator on Bilibili

## Original Creator

**Lacrimosa1337**

Bilibili:

https://space.bilibili.com/68375663

## Official GitHub Repository

https://github.com/Gara-11/photosense

## Repository Maintainer

**Gara-11**

---

# PhotoSense

### AI should become part of the Photoshop workflow — not replace it.

### 让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。

**Open-source AI-assisted generative image editing for professional Photoshop workflows.**
