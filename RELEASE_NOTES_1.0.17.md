# PhotoSense v1.0.17

本次更新重点加入人物细节保护的光影重绘、改进复杂选区工具与多候选结果管理，并增强 Nano Banana 中转接口兼容性。

This release introduces detail-protected relighting, improves complex selection tools and multi-candidate result management, and expands compatibility with Nano Banana relay APIs.

## 光影重绘 / Relighting

- 新增独立光影重绘页面，同时支持 GPT Image 2 与 Nano Banana。  
  Added a dedicated relighting page with support for both GPT Image 2 and Nano Banana.
- 光影描述使用独立提示词，不会改写或影响原有局部重绘提示词。  
  Relighting uses an isolated prompt and does not rewrite or affect the existing inpainting prompt.
- AI 输出只作为低频明暗、阴影方向、高光平衡和色温引导；最终效果在本地叠加回原图。  
  AI output is used only as a low-frequency guide for illumination, shadow direction, highlight balance, and color temperature; the final effect is applied locally to the original image.
- 人物身份、五官、表情、皮肤纹理、发丝、动作、服装设计和物体结构继续使用原图细节，不直接采用 AI 重画像素。  
  Identity, facial features, expression, skin texture, hair strands, pose, clothing design, and object geometry continue to come from the original image rather than AI-repainted pixels.
- 光影强度受“人物 / 细节保护”选项控制，蒙版外像素保持不变。  
  Relighting strength is controlled by the Person / Detail Protection setting, while pixels outside the mask remain unchanged.
- 已移除测试阶段的创成式移除 / 替换页面，正式版不再包含该功能。  
  The experimental generative removal / replacement page has been removed and is not included in the release.

## 结果串联 / Chained Workflows

- 结果页新增“将结果作为下一步输入”，可以在局部重绘完成后继续进行光影重绘，也可以继续下一轮局部重绘。  
  Added “Use Result as Next Input,” allowing an inpainted result to continue into relighting or another inpainting pass.
- 多步骤处理会累计每一步蒙版，并在回传 Photoshop 时合并为一个透明补丁。  
  Multi-step workflows accumulate masks from every step and merge them into one transparent patch when sent back to Photoshop.
- 累计蒙版以外的区域始终继承最初输入图像，不会被后续步骤改写。  
  Areas outside the accumulated mask always inherit the initial input image and are not modified by later steps.

## 选区工具改进 / Selection Tool Improvements

- 新增智能点选，可通过左键添加、右键排除相似物体区域。  
  Added Smart Select with left-click inclusion and right-click exclusion for similar object regions.
- 新增自由套索和磁性套索，适合处理细长、弯曲或不规则物体。  
  Added Freehand Lasso and Magnetic Lasso for thin, curved, or irregular objects.
- 优化智能点选拖尾和磁性套索路径平滑，减少细碎线条与异常延伸。  
  Improved Smart Select trail suppression and Magnetic Lasso path smoothing to reduce fragmented lines and unwanted extensions.
- 新增扩展、收缩、平滑和羽化选区，并支持叠加、黑白和边缘三种蒙版预览。  
  Added mask expansion, contraction, smoothing, and feathering, plus Overlay, Black & White, and Outline preview modes.
- 保留画笔、橡皮、缩放、平移、笔刷大小预览及撤销 / 重做工作流。  
  Preserved brush, eraser, zoom, pan, brush-size preview, and undo / redo workflows.

## 多候选结果 / Multiple Candidates

- 生成张数支持 1–4 张，GPT Image 2 与 Nano Banana 均可使用。  
  Candidate count now supports 1–4 images for both GPT Image 2 and Nano Banana.
- 界面明确提示每张候选会分别调用一次 API，调用次数和费用会相应增加。  
  The interface now clearly states that each candidate triggers a separate API call and increases usage and cost accordingly.
- 结果中心支持切换、收藏、删除和单独重新生成当前候选。  
  The result center supports switching, favoriting, deleting, and regenerating the current candidate independently.

## Nano Banana 中转兼容 / Nano Banana Relay Compatibility

- `generateContent` 自动兼容模式使用标准 `generationConfig.imageConfig.imageSize` 传递 1K / 2K / 4K 分辨率。  
  Auto compatibility for `generateContent` now uses the standard `generationConfig.imageConfig.imageSize` structure for 1K / 2K / 4K requests.
- 支持 Gemini 原生 `inlineData`、OpenAI 风格 `b64_json`、常见 Base64 字段和图片 URL 返回格式。  
  Added support for native Gemini `inlineData`, OpenAI-style `b64_json`, common Base64 fields, and image URL responses.
- 当中转只返回文字、模型不可用、计费规则错误或安全拦截时，会显示更具体的诊断信息。  
  More specific diagnostics are shown when a relay returns text only, reports an unavailable model or pricing rule, or blocks the request for safety reasons.
- 网络中断或响应不完整时不会自动重复生成请求，避免可能发生的重复扣费。  
  PhotoSense does not automatically resend generation requests after a network interruption or truncated response, reducing the risk of duplicate charges.

## 无损保护与兼容性 / Non-Destructive Protection and Compatibility

- 本地蒙版合成仍是最终输出依据；蒙版外像素强制取自当前原图。  
  Local mask compositing remains the final authority; pixels outside the mask are always taken from the current original image.
- 原局部重绘、参考图、Real-ESRGAN、Photoshop 获取与透明补丁回传流程继续保留。  
  Existing inpainting, reference-image, Real-ESRGAN, Photoshop import, and transparent-patch return workflows remain available.
- API Key、模型设置和 UI Scale 继续保存在当前 Windows 用户的 `%LOCALAPPDATA%\PixelPatch Studio`。  
  API keys, model settings, and UI Scale remain stored under `%LOCALAPPDATA%\PixelPatch Studio` for the current Windows user.
- 版本更新为 `1.0.17.0`。  
  The application version has been updated to `1.0.17.0`.

## 下载说明 / Downloads

- `PhotoSense-1.0.17.exe`：Windows 可执行程序。  
  Windows executable.
- `PhotoSense-1.0.17-Source.zip`：可重新构建的完整源码。  
  Complete, reproducible source package.

项目地址 / Project: https://github.com/Gara-11/photosense/
