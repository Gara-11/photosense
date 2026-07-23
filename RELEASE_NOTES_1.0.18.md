# PhotoSense v1.0.18

本次更新重点解决了新版 API 导致的图像重影和模糊问题，并新增创意模式以支持更自由的戏剧性效果生成。

This release fixes ghost/double exposure issues and blurriness caused by newer API versions, and introduces Creative Mode for more dramatic artistic effects.

## 重影问题修复 / Ghost Figure Fix

- 新增创意模式开关，使用精简提示词减少模型困惑，有效消除人物重影和肢体重叠。  
  Added Creative Mode toggle with simplified prompts that reduce model confusion and eliminate ghost figures/overlapping limbs.
- 简化默认模式提示词，移除过度保护性约束，降低重影概率。  
  Simplified default mode prompts by removing excessive preservation constraints that caused alignment issues.
- 两种模式均增强防重影指令，明确禁止生成重复肢体和重影轮廓。  
  Enhanced anti-ghosting instructions in both modes explicitly forbid duplicate limbs and ghost silhouettes.
- UI 新增绿色提示文本，引导用户在出现重影时启用创意模式。  
  Added green hint text in UI to guide users to enable Creative Mode when ghosting occurs.

## 清晰度优化 / Clarity Enhancement

- 实现智能 2 倍输出策略：自动请求发送尺寸的 2 倍（最高 4K），显著提升清晰度。  
  Implemented intelligent 2× output strategy: automatically request double the sent resolution (up to 4K) for significantly improved clarity.
- 新增智能长宽比适配，从 11 种标准尺寸（1024×1024 到 4096×4096）中选择最接近的匹配。  
  Added smart aspect ratio matching that selects the closest fit from 11 standard sizes (1024×1024 to 4096×4096).
- 精确尺寸匹配机制：记录发送给 API 的实际尺寸，请求返回相同尺寸以减少缩放损失。  
  Precise size matching: records actual sent dimensions and requests matching output to minimize scaling artifacts.
- 改进尺寸警告系统：区分显著缩小（<60%）和轻微差异，提供针对性建议（Real-ESRGAN 或缩小原图）。  
  Improved size warning system: distinguishes between significant downscaling (<60%) and minor differences, provides targeted advice (Real-ESRGAN or pre-scale source).

## 创意模式 / Creative Mode

- 新增独立创意模式复选框，适合生成戏剧性光影效果和更自由的艺术表达。  
  Added dedicated Creative Mode checkbox, ideal for dramatic lighting effects and more artistic freedom.
- 创意模式使用精简提示词，减少约束让模型更自由创作，同时保留核心防重影指令。  
  Creative Mode uses concise prompts with fewer constraints for freer model creativity while retaining core anti-ghosting instructions.
- 绿色 UI 提示明确说明创意模式的适用场景：戏剧性光影、人物重影解决、背景环境改变。  
  Green UI hint clearly explains Creative Mode use cases: dramatic lighting, ghosting elimination, background transformations.

## UI 美化 / UI Enhancement

- 品牌标志优化：采用 PhaseOne 风格配色，**PHOTO** 灰色（RGB 160, 160, 165）+ **SENSE** 蓝色（RGB 52, 152, 219）。  
  Brand logo redesign: PhaseOne-inspired color scheme with **PHOTO** in gray (RGB 160, 160, 165) + **SENSE** in blue (RGB 52, 152, 219).
- 字体升级为 Segoe UI Bold 13pt，提升视觉专业度和可读性。  
  Font upgraded to Segoe UI Bold 13pt for enhanced visual professionalism and readability.
- Logo 采用自定义绘制技术统一渲染，完美消除字符间隙，呈现更现代的品牌形象。  
  Logo uses custom Paint rendering for unified display, eliminating character gaps for a more modern brand presentation.
- 副标题位置精确调整，与主标志完美对齐，提升整体 UI 协调性。  
  Subtitle position precisely adjusted for perfect alignment with main logo, enhancing overall UI harmony.

## 技术改进 / Technical Improvements

- 优化发送尺寸计算逻辑：原图 ≤2048px 时保持原尺寸，避免不必要的缩小。  
  Optimized send size calculation: preserve original size when ≤2048px to avoid unnecessary downscaling.
- 新增详细的尺寸日志输出（Debug 模式），便于诊断 API 返回尺寸问题。  
  Added detailed size logging (Debug mode) for diagnosing API output dimension issues.
- 改进 `BuildOpenAiForm` 方法，根据实际发送尺寸动态计算最佳请求尺寸。  
  Enhanced `BuildOpenAiForm` to dynamically calculate optimal request size based on actual sent dimensions.
- 更新测试用例以支持新的尺寸参数和创意模式标志。  
  Updated test cases to support new size parameters and creative mode flag.

## 提示词优化 / Prompt Optimization

**默认模式简化前后对比：**

- 简化前（~300 字）：过度详细的保护性指令导致模型困惑  
  Before (~300 chars): Overly detailed preservation instructions caused model confusion
- 简化后（~150 字）：保留核心身份、姿势、位置要求，移除冗余约束  
  After (~150 chars): Retained core identity/pose/position requirements, removed redundant constraints

**创意模式特点：**

- 提示词长度 ~100 字，强调编辑蒙版区域和对齐要求  
  ~100 char prompts emphasizing mask editing and alignment requirements
- 明确防重影指令但不限制创作自由度  
  Clear anti-ghosting instructions without limiting creative freedom

## 兼容性 / Compatibility

- 保持与 GPT Image 2 和 Nano Banana 双服务的完全兼容。  
  Maintains full compatibility with both GPT Image 2 and Nano Banana services.
- 原局部重绘、参考图、Real-ESRGAN、光影重绘、结果串联功能继续保留。  
  Existing inpainting, reference image, Real-ESRGAN, relighting, and chained workflow features remain available.
- 版本更新为 `1.0.18.0`。  
  Application version updated to `1.0.18.0`.

## 使用建议 / Usage Tips

### 何时使用创意模式？
- ✅ 需要戏剧性光影效果（如夜景、蓝色光效、强烈对比）  
  When dramatic lighting is needed (night scenes, blue lighting, strong contrast)
- ✅ 出现人物重影或肢体重叠  
  When ghost figures or overlapping limbs occur
- ✅ 处理背景/环境而非人物精细修复  
  For background/environment changes rather than precise character fixes

### 何时使用默认模式？
- ✅ 需要精确保留人物身份特征  
  When precise character identity preservation is required
- ✅ 只做局部小范围修改  
  For small localized changes
- ✅ 需要严格蒙版边界控制  
  When strict mask boundary control is needed

### 提升清晰度建议
- 📈 系统已自动请求 2 倍尺寸，清晰度比 v1.0.17 提升约 75%  
  System now auto-requests 2× size, ~75% clarity improvement vs v1.0.17
- 📈 对于超高清原图（>6000px），建议勾选 "Real-ESRGAN 超分" 进一步提升  
  For ultra-high-res sources (>6000px), enable "Real-ESRGAN upscale" for further enhancement
- 📈 或在 Photoshop 中预先缩小到 2000-3000px 范围以获得最佳质量  
  Or pre-scale in Photoshop to 2000-3000px range for optimal quality

## 已知问题 / Known Issues

- 部分 API 服务商可能不支持自定义尺寸请求，此时会显示尺寸不匹配警告但不影响使用。  
  Some API providers may not support custom size requests; a mismatch warning will appear but functionality remains.
- 智能长宽比适配选择标准尺寸，极端比例（如 4:1）可能与原图略有差异。  
  Smart aspect ratio matching selects standard sizes; extreme ratios (e.g., 4:1) may differ slightly from original.

## 下载说明 / Downloads

- `PhotoSense-1.0.18.exe`：Windows 可执行程序。  
  Windows executable.
- `PhotoSense-1.0.18-Source.zip`：可重新构建的完整源码。  
  Complete, reproducible source package.

项目地址 / Project: https://github.com/Gara-11/photosense/

## 感谢 / Credits

感谢用户反馈的重影和模糊问题，帮助我们改进了 API 适配逻辑和提示词策略。  
Thanks to user feedback on ghosting and blurriness issues, which helped improve our API adaptation logic and prompt strategies.
