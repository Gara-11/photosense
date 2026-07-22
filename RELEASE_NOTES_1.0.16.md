# PhotoSense v1.0.16

本次更新重点完善了 Nano Banana 高分辨率生成，并为 GPT Image 2 与 Nano Banana 同时加入可选参考图功能。

This release improves Nano Banana high-resolution generation and adds optional reference-image support for both GPT Image 2 and Nano Banana.

## 新增功能 / New Features

- 新增可选参考图功能，同时支持 GPT Image 2 与 Nano Banana。  
  Added optional reference-image support for both GPT Image 2 and Nano Banana.
- 参考图只用于辅助颜色、风格和材质，不会替换或改写用户输入的效果描述。  
  Reference images are used only to guide color, style, and material. They do not replace or rewrite the user's prompt.
- 对参考图进行本地构图打散和防残影处理，降低参考图中的人物、主体轮廓或动作残留到生成结果中的概率。  
  Reference images receive local layout scrambling and anti-ghost preprocessing to reduce the risk of people, subject outlines, or poses leaking into the generated result.
- 未选择参考图时继续使用原有请求流程，不影响已有局部重绘功能。  
  When no reference image is selected, PhotoSense keeps using the original request path without affecting existing inpainting features.

## Nano Banana 4K 改进 / Nano Banana 4K Improvements

- 新增 Nano Banana 分辨率协议选项：自动兼容、Response Format、Image Config。  
  Added Nano Banana resolution protocol options: Auto Compatibility, Response Format, and Image Config.
- VectorEngine 等中转接口在自动模式下会使用兼容的 Image Config 请求结构。  
  In Auto mode, relay services such as VectorEngine use the compatible Image Config request structure.
- 修正选择 4K 时的参数投递逻辑，避免因请求协议不匹配而退回默认分辨率。  
  Corrected 4K parameter delivery to prevent fallback to the default resolution caused by a protocol mismatch.
- 增加返回图片尺寸检测；当实际结果明显低于所选 2K/4K 时给出提示，便于判断模型或中转服务是否支持目标分辨率。  
  Added output-size validation. PhotoSense now warns when the returned image is clearly below the selected 2K/4K target, helping identify unsupported models or relay services.
- 最终输出分辨率仍取决于所选模型和中转服务是否真正支持 2K/4K。  
  Final output resolution still depends on whether the selected model and relay service genuinely support 2K/4K generation.

## 稳定性与兼容性 / Stability and Compatibility

- GPT Image 2 会按“原图、参考图、蒙版”的独立结构发送请求，蒙版仍只控制原图的重绘区域。  
  GPT Image 2 sends the source image, reference image, and mask as separate parts. The mask continues to control only the source image's repaint region.
- Nano Banana 会将原图、蒙版和参考图作为独立输入发送，并明确限制参考图只用于风格参考。  
  Nano Banana sends the source image, mask, and reference image as separate inputs, with explicit constraints that the reference is style-only.
- 保留本地最终蒙版合成：未选中的像素继续强制使用原图，不会因参考图功能而改变。  
  Local final mask compositing remains enabled: unselected pixels are always taken from the original image and are not changed by the reference-image feature.
- 增加参考图请求结构自动测试，覆盖两个模型的启用与未启用分支。  
  Added automated request-structure tests covering reference-enabled and reference-disabled paths for both providers.
- 简化参考图区域文案，仅显示参考图文件名和尺寸。  
  Simplified the reference-image interface to display only the file name and image dimensions.

## 其他 / Other Changes

- 设置页提供本软件免费开源声明及 GitHub 项目跳转链接。  
  Added a free and open-source declaration plus a direct GitHub project link to the Settings page.
- API Key 和软件设置仍保存在当前 Windows 用户的 `%LOCALAPPDATA%\PixelPatch Studio`，升级或更换 EXE 后会自动沿用。  
  API keys and application settings remain stored under `%LOCALAPPDATA%\PixelPatch Studio` for the current Windows user and are automatically preserved when upgrading or replacing the EXE.

## 下载说明 / Downloads

- `PhotoSense-1.0.16-Windows.zip`：Windows 可执行程序与使用说明。  
  Windows executable and user documentation.
- `PhotoSense-1.0.16-Source.zip`：可重新构建的完整源码。  
  Complete, reproducible source package.

项目地址 / Project: https://github.com/Gara-11/photosense/
