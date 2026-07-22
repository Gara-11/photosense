# PhotoSense

> **AI should become part of the Photoshop workflow — not replace it.**  
> **让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。**

PhotoSense is an open-source AI-powered image editing and generative retouching tool designed for professional **Adobe Photoshop** workflows.

PhotoSense 是一款面向 **Adobe Photoshop 专业后期工作流** 开发的开源 AI 图像编辑与生成式重绘工具。

It combines the professional image-editing capabilities of Photoshop with modern generative AI models, allowing photographers, retouchers, designers, and digital artists to perform controlled AI-assisted editing directly inside an existing post-production workflow.

它将 Photoshop 的专业图像处理能力与现代生成式 AI 图像模型结合，使摄影师、修图师、设计师与数字艺术创作者能够在现有后期工作流中，以更可控的方式使用 AI 完成图像编辑。

PhotoSense can be used for:

PhotoSense 可用于：

- AI Generative Editing / AI 生成式编辑
- Generative Inpainting / 生成式局部修复
- Local Image Regeneration / 局部图像重绘
- Object Replacement / 内容与物体替换
- Detail Reconstruction / 局部细节重建
- Mask-Constrained Editing / 蒙版约束编辑
- AI-Assisted Retouching / AI 辅助修图
- High-Resolution AI Workflows / 高分辨率 AI 工作流

The goal of PhotoSense is not to replace Photoshop.

PhotoSense 的目标并不是取代 Photoshop。

Instead, the project aims to make generative AI another controllable tool inside the Photoshop workflow.

而是让生成式 AI 成为 Photoshop 专业工作流中的一种新工具。

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

## 🎨 Adobe Photoshop Workflow Integration / Adobe Photoshop 工作流集成

PhotoSense is designed to work **with Photoshop**, not replace it.

PhotoSense 的设计目标是**与 Photoshop 协同工作，而不是替代 Photoshop**。

It brings AI generation into an existing professional Photoshop workflow and reduces the need to repeatedly export, upload, download, and re-import images.

它将 AI 生成能力整合进现有的专业 Photoshop 工作流中，减少反复导出、上传、下载和重新导入图像的操作。

A typical PhotoSense workflow can look like this:

典型的 PhotoSense 工作流程如下：

```text
Adobe Photoshop
        ↓
Read Current Image / 读取当前图像
        ↓
Create Selection / 创建选区
Person / Subject / Background
人物 / 主体 / 背景
        ↓
Refine Mask / 精修蒙版
        ↓
GPT Image 2 / Nano Banana
        ↓
AI Generative Editing / AI 局部生成
        ↓
Optional Real-ESRGAN Upscaling
可选 Real-ESRGAN 超分
        ↓
Local Mask Compositing
本地蒙版合成
        ↓
Transparent Patch / 透明补丁
        ↓
Return to Photoshop / 返回 Photoshop
        ↓
Continue Professional Retouching
继续专业后期处理
```

A traditional AI-assisted workflow often requires:

传统 AI 修图流程通常需要：

```text
Photoshop
→ Export Image / 导出图片
→ Upload to AI Platform / 上传 AI 平台
→ Enter Prompt / 输入提示词
→ Generate / 等待生成
→ Download / 下载图片
→ Re-import into Photoshop / 重新导入 Photoshop
```

PhotoSense aims to simplify this into:

PhotoSense 希望将流程简化为：

```text
Photoshop
→ PhotoSense
→ AI Editing / AI 编辑
→ Photoshop
```

---

# 🤖 GPT Image 2 Support / GPT Image 2 支持

PhotoSense supports **GPT Image 2-compatible image editing APIs**.

PhotoSense 支持 **GPT Image 2 兼容的图像编辑 API**。

Possible workflows include:

可用于：

- Mask-Based Generative Editing / 基于蒙版的生成式编辑
- Inpainting / 局部修复
- Local Regeneration / 局部重绘
- Object Replacement / 内容替换
- Detail Reconstruction / 细节补全
- Prompt-Based Editing / 基于 Prompt 的图像编辑
- Selective Region Editing / 指定区域编辑
- AI-Assisted Retouching / AI 辅助修图

Users can configure compatible API parameters according to their provider.

用户可以根据所使用的 API 服务自行配置：

```text
API Key / API 密钥
Base URL / 基础地址
Endpoint / 接口路径
Model / 模型
```

PhotoSense does not require personal API credentials to be hard-coded into the public source code.

PhotoSense 不要求将个人 API Key 硬编码到公开源码中。

---

# 🍌 Nano Banana Support / Nano Banana 支持

PhotoSense supports **Nano Banana / Gemini-compatible image generation APIs**.

PhotoSense 支持 **Nano Banana / Gemini 兼容的图像生成接口**。

Configurable parameters may include:

支持配置：

```text
API Base URL / API 基础地址
API Endpoint / API 接口路径
API Key / API 密钥
Model / 模型
Compatible Relay Service / 兼容的第三方中转服务
```

This allows PhotoSense to work with different compatible API providers instead of being permanently tied to a single backend.

这使 PhotoSense 可以根据实际 API 实现连接不同的兼容服务，而不必永久绑定单一服务商。

Possible use cases include:

可用于：

- Local Image Regeneration / 局部图像重绘
- Prompt-Based Modification / Prompt 驱动图像修改
- Content Generation / 内容生成
- Detail Enhancement / 细节增强
- Image Restoration / 图像修复
- Mask-Constrained Generation / 蒙版约束生成

---

# 🎯 Automatic Selection / 自动选区

PhotoSense can work with Photoshop-based selection capabilities to quickly define AI editing areas.

PhotoSense 可以结合 Photoshop 的选区能力快速建立 AI 编辑区域。

Supported selection targets may include:

支持的目标类型包括：

- Person / 人物
- Subject / 主体
- Background / 背景

After automatic selection, users can continue refining the mask manually.

自动选区完成后，用户仍然可以继续手动精修蒙版。

---

# 🖌️ Mask Editor / 蒙版编辑器

PhotoSense provides a dedicated mask-editing workflow so users can precisely control where AI is allowed to make changes.

PhotoSense 提供独立的蒙版编辑工作流，让用户能够精确控制 AI 被允许修改的区域。

The mask editor supports:

蒙版编辑支持：

- Brush / 画笔添加蒙版
- Eraser / 橡皮擦除蒙版
- Brush Size Adjustment / 调整画笔尺寸
- Zoom / 缩放
- Pan / 平移
- Undo / 撤销
- Redo / 重做
- Manual Mask Refinement / 手动精修蒙版

The core idea is simple:

核心理念非常简单：

> **The user decides exactly where AI is allowed to edit.**  
> **由用户决定 AI 到底允许修改哪里。**

Compared with sending an entire image to an AI generator, a mask-based workflow provides significantly more control.

相比直接将整张图片交给 AI 重新生成，基于蒙版的工作流能够提供更高的可控性。

---

# 🧩 Mask-Constrained Generative Editing / 蒙版约束生成式编辑

One of PhotoSense's core design goals is to restrict AI modifications to user-selected areas as much as possible.

PhotoSense 的核心设计目标之一，是尽可能限制 AI 只修改用户指定的区域。

After AI generation, PhotoSense can perform local mask-based compositing.

AI 生成结束后，PhotoSense 可以根据蒙版重新进行本地图像合成。

```text
Inside Mask / 蒙版内部
→ AI Generated Result / 使用 AI 生成结果

Outside Mask / 蒙版外部
→ Original Image Pixels / 保留原始图像像素
```

This helps reduce unwanted AI changes to areas such as:

这样可以降低 AI 意外修改以下区域的概率：

- Identity / 人物身份
- Face / 面部
- Composition / 构图
- Background / 背景
- Clothing / 服装
- Color / 色彩
- Important Details / 不需要修改的重要细节

---

# 🔄 Non-Destructive Transparent Patch Workflow / 非破坏性透明补丁工作流

PhotoSense can return edited areas to Photoshop as a **Transparent Patch**.

PhotoSense 可以将 AI 编辑后的区域作为 **透明补丁图层（Transparent Patch）** 返回 Photoshop。

The workflow can look like this:

工作流程如下：

```text
Original Photoshop Document
Photoshop 原始文档
        ↓
PhotoSense Reads Image
PhotoSense 读取图像
        ↓
Create / Refine Mask
创建 / 精修蒙版
        ↓
GPT Image 2 / Nano Banana
        ↓
AI Generation
AI 生成
        ↓
Local Mask Compositing
本地蒙版合成
        ↓
Transparent Edited Patch
生成透明编辑补丁
        ↓
Return to Photoshop
返回 Photoshop
```

Inside the returned patch:

在返回的透明补丁中：

```text
Edited Region / 编辑区域
→ AI Result / AI 生成结果

Unedited Region / 未编辑区域
→ Transparent / 保持透明
```

This avoids replacing the entire original image with an AI-generated result.

这样可以避免直接使用 AI 生成图覆盖整张原始图片。

After returning to Photoshop, users can continue working with:

返回 Photoshop 后，用户仍然可以继续使用：

- Layers / 图层
- Layer Masks / 图层蒙版
- Blend Modes / 混合模式
- Opacity / 透明度
- Camera Raw
- Frequency Separation / 高频低频
- Dodge & Burn / 中性灰或双曲线修图
- Liquify / 液化
- Color Adjustments / 色彩调整
- Local Retouching / 局部修图
- Other Professional Photoshop Tools / 其他 Photoshop 专业后期工具

This reflects one of the most important design principles of PhotoSense:

这也是 PhotoSense 最重要的设计理念之一：

> **Make AI part of the Photoshop layer-based workflow.**  
> **让 AI 成为 Photoshop 图层工作流的一部分。**

---

# 🔍 Real-ESRGAN Super-Resolution / Real-ESRGAN 超分辨率

PhotoSense provides optional integration with **Real-ESRGAN** for local AI super-resolution.

PhotoSense 提供可选的 **Real-ESRGAN 本地 AI 超分辨率功能**。

Generated images may be upscaled before final compositing and transfer back into the Photoshop workflow.

AI 图像生成完成后，可以根据工作流程先执行超分辨率处理，再进行最终合成并返回 Photoshop。

The repository includes:

项目中包含：

```text
download-realesrgan.ps1
```

This script is used to help prepare the required Real-ESRGAN components.

该脚本用于帮助准备 Real-ESRGAN 相关组件。

Real-ESRGAN is optional and is not required for the basic AI editing workflow.

Real-ESRGAN 属于可选功能，不影响 PhotoSense 基础 AI 编辑功能的使用。

---

# 🔐 API Credential Security / API 凭据安全

PhotoSense does not recommend hard-coding real API credentials directly into the source code.

PhotoSense 不建议将真实 API Key 直接硬编码到源码中。

On supported Windows systems, API credentials may be protected using Windows user-level encryption mechanisms.

在支持的 Windows 环境中，API 凭据可以通过当前 Windows 用户级别的加密机制进行保护。

Developers should always ensure that sensitive information is not committed to a public repository.

开发者在提交代码前，应始终确保以下敏感信息没有被上传到公开仓库：

```text
API Keys / API 密钥
Access Tokens / 访问令牌
Passwords / 密码
Private Credentials / 私有凭据
Private Endpoints / 私有接口
Secret Files / 密钥文件
```

Do not commit files such as:

请勿提交类似以下文件：

```text
.env
secrets.json
credentials.json
api_keys.json
config.local.json
```

If an API key is ever accidentally committed to a public GitHub repository, it should be revoked and regenerated immediately.

如果 API Key 曾被错误上传到公开 GitHub 仓库，应立即撤销并重新生成。

---

# 🧠 Design Philosophy / 设计理念

The core philosophy of PhotoSense is:

PhotoSense 的核心理念是：

> **AI should become part of the Photoshop workflow — not replace it.**  
> **让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。**

Generative AI is extremely powerful.

生成式 AI 非常强大。

However, professional image editing still requires precise human control over:

但专业图像后期仍然需要创作者对以下内容进行精确控制：

- Composition / 构图
- Color / 色彩
- Layers / 图层
- Masks / 蒙版
- Identity Consistency / 人物一致性
- Local Adjustments / 局部调整
- Retouching / 精修
- Fine Details / 细节
- Final Artistic Control / 最终艺术控制

PhotoSense does not treat AI as a standalone one-click image generator.

PhotoSense 并不把 AI 当作一个独立的一键生图工具。

Instead, it aims to combine:

它希望将：

```text
Professional Photoshop Workflow
专业 Photoshop 工作流

+

Generative AI
生成式 AI
```

into a controllable and non-destructive editing pipeline.

结合成一个可控、非破坏性的专业图像编辑流程。

---

# 🚀 Workflow / 完整工作流程

```text
Adobe Photoshop
      │
      ▼
Import Active Image
读取当前图像
      │
      ▼
Automatic Selection
自动选区
Person / Subject / Background
人物 / 主体 / 背景
      │
      ▼
Manual Mask Refinement
手动精修蒙版
Brush / Eraser / Zoom / Pan
画笔 / 橡皮 / 缩放 / 平移
      │
      ▼
Select AI Backend
选择 AI 后端
GPT Image 2 / Nano Banana
      │
      ▼
Prompt + Mask
提示词 + 蒙版
      │
      ▼
AI Generation
AI 生成
      │
      ▼
Optional Real-ESRGAN Upscaling
可选 Real-ESRGAN 超分
      │
      ▼
Local Mask Compositing
本地蒙版合成
      │
      ▼
Transparent Patch
透明补丁
      │
      ▼
Return to Photoshop
返回 Photoshop
      │
      ▼
Continue Professional Retouching
继续专业后期处理
```

---

# 🖥️ Requirements / 运行环境

PhotoSense currently targets Windows-based Photoshop workflows.

PhotoSense 目前主要面向 Windows 平台下的 Photoshop 工作流。

Recommended environment:

推荐环境：

```text
Windows 10 / Windows 11
Adobe Photoshop
.NET Framework 4.8
```

At least one compatible AI backend is required:

需要至少配置一种兼容的 AI 服务：

```text
GPT Image 2-compatible API
GPT Image 2 兼容 API

or / 或

Nano Banana / Gemini-compatible API
Nano Banana / Gemini 兼容 API
```

Optional:

可选：

```text
Real-ESRGAN
```

for local AI super-resolution.

用于本地 AI 超分辨率处理。

---

# 🛠️ Building from Source / 从源码构建

Clone the repository:

克隆仓库：

```bash
git clone https://github.com/Gara-11/photosense.git
```

Enter the project directory:

进入项目目录：

```bash
cd photosense
```

The project is primarily written in:

项目主要使用：

```text
C#
.NET Framework 4.8
```

The current project file is:

当前项目文件为：

```text
PixelPatchStudio.csproj
```

> The internal project may still contain historical development naming.  
> The public software and project name is **PhotoSense**.
>
> 项目内部目前可能仍保留早期开发阶段使用的历史命名。  
> 软件正式公开名称为 **PhotoSense**。

The repository also includes:

仓库中还包含：

```text
build.ps1
```

for build-related tasks.

用于执行与项目构建相关的操作。

Depending on the Windows PowerShell execution policy, local script execution may need to be enabled before running PowerShell scripts.

根据 Windows PowerShell 的执行策略设置，运行本地 PowerShell 脚本前可能需要允许本地脚本执行。

---

# 📁 Project Structure / 项目结构

```text
photosense/
│
├── assets/
│   └── Application Resources / 应用资源
│
├── src/
│   └── PhotoSense Source Code / PhotoSense 源代码
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

Build-generated directories should normally not be committed:

编译生成的目录通常不应提交到源码仓库：

```text
bin/
obj/
outputs/
```

---

# 🔑 API Configuration / API 配置

Depending on the selected AI provider, users may need to configure:

根据所使用的 AI 服务商，用户可能需要配置：

```text
API Key / API 密钥
Base URL / 基础地址
Endpoint / 接口路径
Model / 模型
```

PhotoSense may also support compatible third-party API relay services depending on the implementation.

根据 API 实现方式，PhotoSense 也可以支持兼容的第三方 API 中转服务。

Third-party APIs are independent services.

第三方 API 属于独立服务。

Users are responsible for:

用户需要自行负责：

- API Credentials / API 凭据
- API Costs / API 费用
- API Usage Policies / API 使用政策
- Model Availability / 模型可用性
- Service Availability / 服务稳定性
- Third-Party Terms of Service / 第三方服务条款

Using a third-party API through PhotoSense does not imply endorsement of that provider by the PhotoSense project.

通过 PhotoSense 使用某个第三方 API，并不代表 PhotoSense 项目对该服务商进行官方背书。

---

# 👤 Creator & Maintainer / 创作者与维护者

## Original Creator / 原作者

### **Lacrimosa1337**

**Lacrimosa1337 is the original creator and primary author of PhotoSense.**

**Lacrimosa1337 是 PhotoSense 的真正原作者与主要创作者。**

The original concept, software design, core implementation, and primary development of PhotoSense were created by **Lacrimosa1337**.

PhotoSense 的以下主要内容均由 **Lacrimosa1337** 创作或完成：

- Original Concept / 原始创意
- Software Design / 软件设计
- Core Implementation / 核心实现
- Main Application / 主体程序
- Primary Development / 主要开发工作

### Original Creator's Social Account / 原作者社交账号

**Bilibili / 哔哩哔哩**

https://space.bilibili.com/68375663

This Bilibili account belongs to the original creator of PhotoSense:

该 Bilibili 账号属于 PhotoSense 原作者：

```text
Lacrimosa1337
```

---

## GitHub Publisher & Repository Maintainer / GitHub 代发布与仓库维护者

### **Gara-11**

Gara-11 is responsible for publishing and maintaining the GitHub repository on behalf of the project.

Gara-11 主要负责代为发布并维护 PhotoSense 的 GitHub 项目仓库。

Responsibilities include:

主要职责包括：

- Publishing the Source Repository / 代为发布源码仓库
- Maintaining the GitHub Repository / GitHub 仓库维护
- Organizing Releases / 整理与发布版本
- Maintaining README and Documentation / README 与文档维护
- Assisting with Issues and Pull Requests / 协助管理 Issues 与 Pull Requests
- Assisting with Project Distribution / 协助软件分发
- Community and Repository Management / 协助社区及项目维护

**Gara-11 is not the original creator of PhotoSense.**

**Gara-11 并非 PhotoSense 原作者。**

GitHub:

https://github.com/Gara-11

---

# 🌐 Official Project Links / 官方项目渠道

## Official GitHub Repository / 官方 GitHub 仓库

https://github.com/Gara-11/photosense

## Original Creator / 原作者

```text
Lacrimosa1337
```

## Original Creator's Bilibili / 原作者 Bilibili

https://space.bilibili.com/68375663

## GitHub Repository Maintainer / GitHub 仓库维护者

```text
Gara-11
```

Please use the official channels above to verify:

请优先通过以上官方渠道核实：

- Authorship / 原作者身份
- Official Source Code / 官方源码
- Project Announcements / 项目公告
- Releases / 正式版本
- Updates / 版本更新
- Official Statements / 官方声明

---

# 🚫 Rebranding, Impersonation & Misleading Distribution Notice  
# 🚫 关于套皮、冒充原作者及误导性分发的声明

PhotoSense is distributed under the:

PhotoSense 采用以下许可证发布：

**GNU General Public License v3.0 (GPL-3.0)**

Open source does **not** mean that third parties may falsely claim authorship, hide the origin of the project, violate GPL obligations, or impersonate the official PhotoSense project.

**开源并不意味着第三方可以冒充原作者、隐瞒项目来源、违反 GPL 义务，或者冒充 PhotoSense 官方项目。**

The original creator of PhotoSense is:

PhotoSense 的真正原作者为：

```text
Lacrimosa1337
```

The original creator's Bilibili account is:

原作者 Bilibili：

```text
https://space.bilibili.com/68375663
```

The official GitHub repository is currently published and maintained by:

当前 GitHub 仓库由以下账号代为发布并维护：

```text
Gara-11
```

Official repository:

官方仓库：

```text
https://github.com/Gara-11/photosense
```

---

## Prohibited or Opposed Practices / 项目明确反对的行为

The PhotoSense project explicitly opposes misleading or abusive practices including, but not limited to:

PhotoSense 项目明确反对以下误导性或滥用行为，包括但不限于：

- Removing or hiding the identity of the original creator  
  删除、隐藏或篡改原作者 **Lacrimosa1337** 的身份信息

- Falsifying authorship  
  虚假声明软件作者身份

- Claiming PhotoSense or substantially derived code as completely independent original software  
  将 PhotoSense 或大量基于 PhotoSense 的代码谎称为完全独立原创的软件

- Making only superficial changes to the name, logo, colors, interface, or branding and intentionally hiding the project's origin  
  仅修改名称、Logo、配色、界面或品牌元素后，故意隐瞒项目真实来源

- Rebranding or "reskinning" PhotoSense while misleading users into believing it was independently developed  
  对 PhotoSense 进行简单“套皮”后，让用户误认为其是完全独立开发的软件

- Presenting an unofficial fork or derivative build as an official PhotoSense release  
  将非官方 Fork 或衍生版本冒充 PhotoSense 官方版本

- Impersonating the original creator or official project  
  冒充原作者或 PhotoSense 官方项目

- Falsely claiming official authorization, certification, partnership, cooperation, or endorsement  
  虚假声称获得官方授权、认证、合作或原作者背书

- Using the PhotoSense name, branding, screenshots, visual identity, or author identity in a misleading commercial manner  
  使用 PhotoSense 名称、品牌、截图、视觉资产或作者身份进行误导性商业宣传

- Removing copyright, license, attribution, or GPL notices that are required to remain  
  删除依法或 GPL 要求保留的版权、许可证、署名或来源信息

- Distributing GPL-covered derivative works as proprietary closed-source software in violation of GPL-3.0  
  将受 GPL-3.0 约束的衍生作品违规作为闭源专有软件发布

- Refusing to provide corresponding source code where GPL-3.0 requires it  
  在 GPL-3.0 要求提供对应源码时拒绝提供源码

- Selling a superficially modified or repackaged version while intentionally hiding that it is based on PhotoSense  
  将简单修改、换皮或重新打包后的版本进行收费，同时故意隐瞒其基于 PhotoSense 的事实

- Using information asymmetry to market PhotoSense-derived software as "fully independently developed"  
  利用信息差，将 PhotoSense 衍生软件宣传为“完全自主研发”

---

# ⚠️ About Paid Distribution / 关于收费分发

PhotoSense currently uses the **GPL-3.0** license.

PhotoSense 当前采用 **GPL-3.0** 许可证。

GPL-3.0 permits charging money for distributing software copies or providing related services, provided that all applicable GPL requirements are followed.

GPL-3.0 在遵守其全部适用要求的前提下，允许对软件副本的分发或相关服务收取费用。

Therefore:

因此：

> **Charging money does not automatically violate GPL-3.0.**  
> **收费本身并不自动违反 GPL-3.0。**

However:

但是：

> **Charging money does not grant the right to hide the original creator.**  
> **允许收费，不代表允许隐藏原作者。**

> **Charging money does not allow false claims of authorship.**  
> **允许收费，不代表允许冒充原创作者。**

> **Charging money does not allow the true origin of PhotoSense to be deliberately concealed.**  
> **允许收费，不代表允许故意隐瞒 PhotoSense 的真实来源。**

> **Charging money does not allow GPL-covered derivative works to be illegally closed-source.**  
> **允许收费，不代表允许将 GPL 覆盖的衍生作品违规闭源。**

> **Charging money does not allow required license or copyright notices to be removed.**  
> **允许收费，不代表允许删除依法或 GPL 要求保留的许可证与版权信息。**

> **Charging money does not allow an unofficial derivative to impersonate an official PhotoSense release.**  
> **允许收费，不代表允许非官方衍生版本冒充 PhotoSense 官方版本。**

> **Charging money does not allow a simple reskin to be falsely advertised as fully independent original software.**  
> **允许收费，不代表可以将简单套皮后的版本谎称为完全自主研发的软件。**

---

# 🚨 Official Identity Notice / 官方身份声明

Unless explicitly authorized by the original creator or officially announced through the official repository, third parties must not falsely claim that their version is:

除非得到原作者明确授权，或通过官方 GitHub 仓库正式发布授权声明，否则第三方不得虚假声称其版本属于：

```text
Official PhotoSense
PhotoSense 官方版

PhotoSense Official Edition
PhotoSense 官方版本

PhotoSense Official Pro
PhotoSense Pro 官方版

PhotoSense Premium Official Edition
PhotoSense Premium 官方版

PhotoSense Authorized Edition
PhotoSense 官方授权版

PhotoSense Official Partner Edition
PhotoSense 官方合作版

Officially Authorized by PhotoSense
PhotoSense 官方授权

Officially Authorized by Lacrimosa1337
Lacrimosa1337 官方授权
```

Forks and derivative projects should clearly distinguish themselves from the official project.

Fork 或衍生项目应明确说明其与官方项目的区别。

Recommended wording:

推荐使用类似以下声明：

```text
This is an unofficial derivative project based on PhotoSense.

这是一个基于 PhotoSense 修改的非官方衍生项目。
```

Derivative projects must not use names, logos, screenshots, marketing pages, payment pages, or other presentation methods that could reasonably mislead users into believing that they are official PhotoSense releases.

衍生项目不得通过名称、Logo、截图、宣传页面、收费页面或其他展示方式，使普通用户误认为其属于 PhotoSense 官方发布。

---

# 🤝 Contributing / 参与贡献

Contributions are welcome.

欢迎任何形式的合理开源贡献。

You can contribute by:

你可以通过以下方式参与：

- Reporting Bugs / 报告 Bug
- Opening Issues / 提交 Issue
- Suggesting Features / 提出新功能建议
- Improving Documentation / 改进文档
- Submitting Pull Requests / 提交 Pull Request
- Improving Photoshop Integration / 改进 Photoshop 集成
- Adding Compatible AI Backend Support / 增加兼容 AI 后端支持
- Improving Mask Workflows / 改进蒙版工作流
- Improving High-Resolution Workflows / 改进高分辨率图像工作流
- Fixing Bugs / 修复问题
- Improving Performance / 优化性能

For major changes, it is recommended to open an Issue first and discuss the proposal before implementation.

对于较大的功能修改，建议先提交 Issue 讨论方案，再进行开发。

Contributors must not commit personal API credentials, private keys, passwords, or other sensitive information.

贡献者不得提交个人 API Key、私钥、密码或其他敏感信息。

---

# 🗺️ Roadmap / 开发路线

Possible future development directions include:

未来可能的开发方向包括：

- Improved Photoshop Integration / 改进 Photoshop 集成
- Additional Compatible Generative Models / 支持更多兼容生成式模型
- Better API Provider Management / 更完善的 API 服务商管理
- Improved Mask Editing / 改进蒙版编辑体验
- Better High-Resolution Workflows / 改进高分辨率图像工作流
- Preset Management / 预设管理
- Model Management / 模型管理
- Simplified Installation / 简化安装流程
- Automatic Update Support / 自动更新支持
- Performance Optimization / 性能优化
- Better Error Handling / 改进错误处理
- Improved UI / UX / 改进界面与交互体验
- Additional Professional Retouching Workflows / 增加更多专业修图工作流

The roadmap may change as the project develops.

项目路线图可能随着实际开发情况进行调整。

---

# 📸 Screenshots & Demonstrations / 软件截图与演示

Screenshots, before-and-after examples, workflow demonstrations, and usage tutorials may be added in future releases.

后续版本可能会加入：

- 软件界面截图
- Before / After 对比
- AI 重绘案例
- Photoshop 联动演示
- 蒙版编辑演示
- 使用教程
- 视频演示

---

# ⚠️ Disclaimer / 免责声明

PhotoSense is an independent open-source project.

PhotoSense 是一个独立的开源项目。

Adobe Photoshop is a product and trademark of Adobe Inc.

Adobe Photoshop 是 Adobe Inc. 的产品及商标。

OpenAI, GPT, and related products or services belong to their respective rights holders.

OpenAI、GPT 及相关产品或服务的权利归其相应权利所有者所有。

Gemini, Google, and related products or services belong to their respective rights holders.

Gemini、Google 及相关产品或服务的权利归其相应权利所有者所有。

Real-ESRGAN is a separate third-party open-source project maintained by its respective authors and contributors.

Real-ESRGAN 是独立的第三方开源项目，由其相应作者和贡献者维护。

Unless explicitly stated otherwise, PhotoSense is not officially affiliated with, endorsed by, sponsored by, or partnered with:

除非另有明确声明，否则 PhotoSense 与以下公司、组织或项目不存在官方隶属、背书、赞助或合作关系：

- Adobe
- OpenAI
- Google
- Real-ESRGAN Developers / Real-ESRGAN 开发者
- Third-Party API Providers / 第三方 API 服务商

Users are responsible for complying with:

用户需要自行确保遵守：

- Third-Party API Terms / 第三方 API 服务条款
- Model Usage Policies / 模型使用政策
- Copyright Laws / 著作权与版权法律
- Privacy Laws / 隐私相关法律
- Applicable Local Laws / 所在地区适用法律
- API Billing Requirements / API 计费规则
- Third-Party Service Rules / 第三方服务规则

Use of a third-party service through PhotoSense does not imply endorsement of that provider by the original creator or the PhotoSense project.

通过 PhotoSense 使用第三方服务，并不代表 PhotoSense 原作者或项目对该服务商进行官方背书。

---

# 📄 License / 开源许可证

PhotoSense is licensed under:

PhotoSense 使用以下许可证：

## GNU General Public License v3.0

Under the applicable terms of GPL-3.0, users may:

在遵守 GPL-3.0 相应条款的前提下，用户可以：

- Use the Software / 使用软件
- Study the Source Code / 学习源代码
- Modify the Software / 修改软件
- Fork the Project / Fork 项目
- Redistribute the Software / 再分发软件
- Distribute Modified Versions / 发布修改版本

Any distribution or derivative work covered by GPL-3.0 must comply with the applicable obligations of the license.

任何受 GPL-3.0 约束的分发行为或衍生作品，都必须遵守该许可证适用的相关义务。

For the complete license terms, see:

完整许可证内容请查看：

```text
LICENSE
```

---

# ❤️ Support the Original Project / 支持原始项目

If PhotoSense is useful to you, please consider supporting the original project.

如果 PhotoSense 对你有所帮助，欢迎通过以下方式支持原始项目：

- ⭐ Star the Official GitHub Repository / 给官方 GitHub 仓库点 Star
- 🐛 Report Bugs through GitHub Issues / 通过 GitHub Issues 报告 Bug
- 🔧 Contribute Improvements / 参与代码与功能改进
- 🔀 Submit Pull Requests / 提交 Pull Request
- 📢 Share the Official Repository / 分享官方项目仓库
- 🚫 Avoid Misleading Repackaged Versions / 避免传播来源不明或误导性的套皮版本
- 📺 Follow the Original Creator on Bilibili / 关注原作者 Bilibili

## Original Creator / 原作者

**Lacrimosa1337**

Bilibili / 哔哩哔哩：

https://space.bilibili.com/68375663

## Official GitHub Repository / 官方 GitHub 仓库

https://github.com/Gara-11/photosense

## GitHub Publisher & Repository Maintainer / GitHub 代发布与仓库维护

**Gara-11**

---

# PhotoSense

### AI should become part of the Photoshop workflow — not replace it.

### 让 AI 成为 Photoshop 工作流的一部分，而不是取代 Photoshop。

**Open-source AI-assisted generative image editing for professional Photoshop workflows.**

**面向专业 Photoshop 工作流的开源 AI 辅助生成式图像编辑工具。**
