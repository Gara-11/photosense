# photosense
PhotoSense

PhotoSense 是一款面向 Adobe Photoshop 专业工作流开发的开源 AI 图像编辑与生成式重绘工具。

它的目标是将传统 Photoshop 后期处理流程与新一代生成式 AI 图像模型结合，让摄影师、修图师、设计师和数字艺术创作者能够在不破坏原始图像和 Photoshop 图层结构的情况下，对图像中的指定区域进行 AI 重绘、局部修复、内容替换与生成式编辑。

PhotoSense 目前支持 GPT Image 2 与 Nano Banana 兼容的图像生成 API，并支持自定义 API 地址、接口路径、模型以及第三方中转服务。

用户可以直接从 Photoshop 当前打开的文档中获取图像，无需反复手动导出文件。PhotoSense 可以调用 Photoshop 自身的主体识别能力，自动创建人物、主体或背景蒙版，同时提供独立的蒙版编辑画布，让用户通过画笔和橡皮进一步精确控制 AI 可以修改的区域。

与直接使用 AI 重新生成整张图片不同，PhotoSense 采用了一套以 蒙版约束和非破坏性编辑 为核心的工作流程。

AI 只负责生成指定区域的内容，生成完成后，PhotoSense 会在本地重新执行像素级蒙版合成，确保蒙版之外的区域继续使用原始图像像素。最终结果可以作为一个蒙版外完全透明的独立补丁图层重新发送到原始 Photoshop 文档中，而不会覆盖原始图层或直接修改源图像。

这意味着用户仍然可以继续使用 Photoshop 的图层、蒙版、混合模式、Camera Raw、频率分离、液化以及其他专业后期工具，对 AI 生成结果进行进一步调整。

PhotoSense 同时集成了可选的 Real-ESRGAN 本地 AI 超分辨率功能，可以在 AI 图像生成完成后自动进行超分处理，以提高生成区域的分辨率与细节表现，再将结果重新合成并发送回 Photoshop。

主要功能

Photoshop 深度工作流集成

直接读取 Photoshop 当前文档，并将 AI 处理结果重新发送回原始 Photoshop 文件，减少传统“导出图片 → 上传 AI → 下载 → 再导入 Photoshop”的重复操作。

GPT Image 2 API 支持

支持通过 GPT Image 2 兼容 API 对指定蒙版区域执行生成式编辑、局部重绘与图像修复。

Nano Banana API 支持

支持 Nano Banana / Gemini 图像生成兼容接口，并兼容可配置的第三方 API 中转服务。

自定义 API 与模型

用户可以自行配置：

API Base URL
Endpoint
Model
API Key
第三方中转接口

因此 PhotoSense 并不局限于单一固定 API 服务，可以根据不同服务商或后续模型进行扩展。

Photoshop 自动智能选区

可以利用 Photoshop 自身的“选择主体”能力自动识别：

人物
主体
背景

快速建立 AI 重绘蒙版。

高性能手动蒙版画布

用户可以进一步通过：

画笔添加区域
橡皮擦除区域
调整画笔尺寸
缩放
平移
撤销 / 重做

精确控制 AI 可以修改的位置。

非破坏性 AI 重绘

PhotoSense 的核心原则不是让 AI 替换整张图片。

AI 完成生成后，软件会再次在本地进行硬蒙版合成：

蒙版内部 → 使用 AI 生成结果
蒙版外部 → 强制保留原始图像像素

从而尽可能避免 AI 意外修改人物身份、构图、背景或其他不需要变化的区域。

透明补丁回写 Photoshop

AI 处理完成后，可以只将修改区域作为：

Transparent Patch / 透明补丁图层

发送回 Photoshop。

蒙版之外完全透明，因此原始 Photoshop 图层不会被覆盖，可以继续自由调整 AI 图层的位置、透明度、蒙版和混合方式。

Real-ESRGAN AI 超分

PhotoSense 支持自动部署和调用 Real-ESRGAN，在生成完成后对 AI 图像执行本地超分辨率处理，提高生成内容的细节和分辨率。

API Key 本地加密

API Key 不直接保存在程序目录或源码中，而是通过 Windows 当前用户级加密机制进行保护，降低因公开源码或移动软件目录导致密钥泄露的风险。

面向专业高分辨率图像工作流

PhotoSense 的设计方向并不仅仅是普通 AI 生图，而是更加偏向：

专业摄影
商业修图
Cosplay 摄影
人像后期
广告视觉
数字艺术
概念设计
高分辨率 Photoshop 工作流






English
PhotoSense

PhotoSense is an open-source AI-powered image editing and generative retouching tool designed to integrate directly with Adobe Photoshop workflows.

It bridges traditional professional image editing with modern generative AI, allowing photographers, retouchers, designers, and digital artists to selectively regenerate or modify specific areas of an image without disrupting the rest of the original document.

PhotoSense currently supports GPT Image 2 and Nano Banana-compatible image generation APIs, with configurable API endpoints, models, and third-party relay services. Users can retrieve the active image directly from Photoshop, automatically generate masks for people, subjects, or backgrounds using Photoshop's built-in selection capabilities, and further refine those masks through PhotoSense's dedicated painting canvas.

Instead of simply replacing an entire AI-generated image, PhotoSense uses a mask-constrained, non-destructive editing workflow. Only the selected region is used to create the final modification, while pixels outside the mask are locally preserved from the original image. The generated result can then be transferred back into the original Photoshop document as a new transparent patch layer, keeping the existing document and original layers intact.

PhotoSense also includes optional Real-ESRGAN super-resolution integration, allowing generated results to be automatically upscaled before being composited back into the Photoshop workflow.

Key Features
Adobe Photoshop integration — Import the active Photoshop document directly into PhotoSense and send generated results back without manually exporting and importing files.
GPT Image 2 support — Perform mask-based AI image editing and generative inpainting using compatible Image API endpoints.
Nano Banana support — Supports Nano Banana / Gemini-compatible image generation APIs and configurable relay endpoints.
Custom API configuration — Configure API base URLs, endpoints, models, and compatible third-party API relay services.
Automatic AI masking — Use Photoshop's subject-selection capabilities to automatically create masks for people, subjects, or backgrounds.
Manual mask refinement — Paint, erase, resize brushes, zoom, pan, undo, and redo selections using a dedicated image-editing canvas.
Non-destructive editing — Only masked regions are modified; unselected pixels are preserved locally from the original image.
Transparent patch workflow — Send only the edited region back to Photoshop as a transparent patch layer instead of replacing the original image.
Real-ESRGAN upscaling — Optional automatic deployment and integration of Real-ESRGAN for local AI super-resolution.
Local encrypted API credentials — API keys are protected using Windows user-level encryption and are never stored directly in the source directory.
High-resolution workflow support — Designed for professional photography, retouching, digital art, and high-resolution Photoshop documents.
Open source and extensible — The architecture can be extended to support additional generative image models and API providers in the future.
Philosophy

PhotoSense is designed around a simple idea:

AI should become part of the Photoshop workflow — not replace it.

Instead of treating generative AI as a separate image generator, PhotoSense uses AI as another editing tool inside a professional post-production pipeline. The goal is to combine Photoshop's precise selection, compositing, layer management, and manual retouching capabilities with the generative power of modern image models.
