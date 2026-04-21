# Phase10 主光阴影 Debug View 收敛与中文化

Date: `2026-04-21`

## 概要

这个阶段主要整理 `Final Shadow Source Tint` 的调试语义，并补齐 `NewWorldRP` Inspector 中主光阴影调试说明的中文文案。

目标不是修改主光阴影本身的 cached / realtime 渲染结果，而是把调试表现收敛到更直观、更稳定的版本：

- Frame Debugger 按 `Per Camera -> NWRPRenderer.Execute -> Stage / Main Light Shadows -> Pass` 的层级查看
- `Final Shadow Source Tint` 只用于区分可见投影物体本体
- 接收面的普通阴影保持原本的黑色
- 去掉黄色的阴影重叠调试，避免干扰 `Cube`、`Sphere (7)` 这类物体本体着色
- 没有参与 `ShadowCaster` 路径的物体不进入这个调试覆盖
- Inspector 中与 cached shadow / debug view 相关的提示统一改成中文

## 对外表现

`Final Shadow Source Tint` 当前的最终语义：

- 蓝色：动态投影物体表面
- 绿色：静态投影物体表面
- 接收面的阴影保持原本的黑色
- 不再显示黄色的静态 / 动态阴影重叠调试

可见物体的蓝 / 绿分类仍然沿用现有配置：

- `Static Caster Layer Mask`
- `Dynamic Caster Layer Mask`

这次没有新增新的 pipeline asset 配置项，也没有改动已有 enum 或 public API。

## Frame Debugger 结构整理

这个阶段也同步整理了 Frame Debugger 中的主光阴影与主渲染阶段层级，目标是让阴影 pass 和不透明 / 透明物体绘制的归属关系更清晰。

当前的层级约定：

- 顶层按相机显示：`NWRP.RenderSingleCamera: <camera.name>`
- 相机下统一进入：`NWRPRenderer.Execute`
- `NWRPRenderer.Execute` 下继续拆分为：
  - `Setup Camera`
  - `Setup Lights`
  - `Main Light Shadows`
  - `Before Rendering`
  - `Main Rendering Opaque`
  - `Main Rendering Transparent`
  - `Submit`

主光阴影相关 pass 统一归到 `Main Light Shadows` 下面：

- `Render Main Light Realtime Cascades`
- `Render Main Light Cached Shadow`
- `Render Main Light Dynamic Overlay`
- `Upload Main Light Disabled Globals`

主场景绘制相关 pass 保持挂在各自的主渲染阶段下：

- `Draw Opaque Objects`
- `Draw Outline Objects`
- `Draw Skybox`
- `Draw Transparent Objects`

为了保证 Frame Debugger 里 `RenderLoop.Draw` 不会错误地继续挂在 `Main Light Shadows` 之下，这次把 stage / pass 的 sample 包裹改成了显式 begin / end 后立刻 flush 的方式。这样主光阴影 sample 结束后，不透明物体绘制会稳定落在 `Main Rendering Opaque -> Draw Opaque Objects` 下，而不是继续被前一个阴影父级 sample 吞进去。

## 运行时实现整理

`Final Shadow Source Tint` 现在被收敛成一个 caster-only 的 debug 视图：

- shared lighting 路径不再根据 `MainLightShadowResult` 给 receiver 表面注入黄色 overlap 调试色
- 主光阴影的真实采样仍然保留，用于正常 `shadowAttenuation`
- 隐藏的 caster overlay shader 只在非阴影像素输出蓝 / 绿对象分类色
- 如果当前像素处于阴影中，overlay 会直接让路，保留底层原本的黑色阴影结果

overlay 的筛选规则也收紧为真正参与阴影投射的对象：

- 仍通过 `ShadowCaster` 路径驱动
- 没有 `ShadowCaster` pass 的 `Unlit` 或其他 shader，不会被这个调试覆盖改色

## 本阶段修正的问题

1. `Sphere (7)` 本体会被黄色 overlap 调试污染

- 根因：caster overlay shader 自己又根据阴影交叠结果输出黄色，导致物体表面和 receiver 语义混在一起
- 修正：去掉黄色 overlap 输出，只保留蓝 / 绿对象分类色

2. `Ground` 上接收的阴影会被自身绿色 debug 覆盖

- 根因：`Ground` 本身也属于静态投影层，overlay 直接覆盖了接收面上的普通阴影区域
- 修正：overlay shader 在阴影像素上直接透明，让底层黑色阴影保留

3. Inspector 中 cached shadow / debug view 提示仍是英文

- 根因：`DrawMainLightShadowInfo()` 与 `Debug View` 下的 `HelpBox` 文案没有本地化
- 修正：将相关提示统一改为中文，保持原有技术语义不变

## 编辑器文案整理

Inspector 中这两组提示已经同步中文化：

- cached shadow 状态与失效阈值说明
- `Final Shadow Source Tint` 的图例、相机范围、globals 上传说明，以及 `ShadowCaster` 参与条件说明

这次只改文案内容，不调整 Inspector 布局和配置字段。

## 验证记录

本阶段完成后的检查结果：

- Unity 刷新编译通过
- Console 保持 `0` error
- `Final Shadow Source Tint` 开启时：
  - `Cube` 维持蓝色对象分类
  - `Sphere (7)` 维持绿色对象分类
  - 接收面的普通阴影保持黑色
  - 黄色 overlap 调试已移除
- 最终已将 debug view 恢复回 `Off`

## 后续可选项

这次仍然保留了“按 `Static Caster Layer Mask` / `Dynamic Caster Layer Mask` 给所有 caster 着色”的规则。

如果后续希望进一步收窄调试范围，可以继续评估：

- 是否要排除 `Ground` 这类主要作为 receiver 的物体，不给它们显示绿色对象分类
- 是否要为 debug tint 增加更细的对象过滤，而不是完全依赖 layer mask
