# 更新日志 / Changelog

## 中文

<details>
<summary><strong>0.1.0</strong></summary>

### 血条外观

- 新增分段血条，超过 12 血自动切换为连续条。
- 低血量变红，受击显示延迟扣血效果。

### 血量显示

- 修复满血判断错误及受伤血条隐藏问题。
- 修正此前版本的错误怪物血量问题。
- 满血显示下，不再显示无法击杀的 Hygrodere 和蝗虫群血条。

</details>

<details>
<summary><strong>0.0.2</strong></summary>

- 新增可选的主机权威血量同步，主机端模组修改怪物血量时客机也能正确显示。
- 客机根据主机快照校正本地预测血量，使显示与本地受击判定保持一致。
- 避免在主机端改血生效前短暂显示原版血量。
- 客机本地预测血量归零但主机尚未确认死亡时，血条保持显示。
- 改进对血量被调高或调低的怪物的最大血量判断。
- 改进 Butler、Maneater 与 Masked 的原版特殊处理。

</details>

<details>
<summary><strong>0.0.1</strong></summary>

- 新增怪物血条。
- 新增横向血条、身旁竖条与纯数字三种显示方式。
- 新增"当前 / 满血"、仅当前血量与百分比三种数字格式。
- 新增根据实际观察判断最大血量。
- 配置修改后即时生效。
- 可选 LethalConfig 支持。
- 检测到 LC_Chinese_Project 时使用中文配置说明。
- 新增可选的调试诊断与测试血条。

</details>

## English

<details>
<summary><strong>0.1.0</strong></summary>

### Bar style

- Added a segmented health bar that switches to a continuous bar above 12 health.
- Low health turns red, and hits show a delayed health drain.

### Health values

- Fixed wrong full-health detection and bars of damaged enemies being hidden.
- Fixed wrong enemy health values from earlier versions.
- With full-health bars enabled, the unkillable Hygrodere and roaming locusts no longer show bars.

</details>

<details>
<summary><strong>0.0.2</strong></summary>

- Added optional host-authority health sync for lobbies where host-side mods change enemy health values.
- Clients rebase their local predicted health on host snapshots, keeping the display and local hit behavior aligned.
- Avoids briefly showing vanilla health before host-side health changes apply.
- Keeps bars on alive enemies whose local predicted health reaches zero before the host confirms death.
- Improved max health handling for enemies with increased or reduced health.
- Improved vanilla special cases for the Butler, Maneater and Masked.

</details>

<details>
<summary><strong>0.0.1</strong></summary>

- Added enemy health bars.
- Added horizontal bar, side vertical bar and numbers-only display modes.
- Added current / max, current-only and percent health text.
- Added max health from observed enemy health.
- Settings apply immediately after changes.
- Optional LethalConfig support.
- Chinese configuration text when LC_Chinese_Project is detected.
- Added optional debug diagnostics and a test bar.

</details>
