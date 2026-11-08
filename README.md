# dsh-finish-notify

DeepSeek Harness 的**完成提醒**插件：请求跑完时，在屏幕角落滑出一个白色小卡片，点一下回到 DSH。

> 灵感与插件规范参照 [dsh-pet](https://github.com/PC2005-cloud/dsh-pet)。

## 特性

- 滑入 / 滑出动画（时长可调，默认 800ms / 420ms）
- **DSH 在前台时不打扰**；已弹出后切回 DSH 会自动收起
- 位置四角可选（右下 / 左下 / 右上 / 左上）+ 边距微调
- 标题、副标题、宽高、停留时长全部可改
- 支持在框左侧插入自定义图片
- 点击卡片 → 把 DSH 窗口拉到前台；右上角 ✕ → 关闭
- 全部配置存在 `~/.dsh/dsh-notify/config.ini`，升级不覆盖

## 安装

**方式一：命令行**

```bash
dsh plugin --profile desktop add dsh-finish-notify
```

**方式二：插件市场**

设置 → 插件市场 → 自定义安装，填入本包路径或仓库地址。

**方式三：手动**

把整个目录放进 `~/.dsh/profiles/desktop/node_modules/dsh-notify`，
并在该 profile 的 `package.json` 的 `dsh.profile.bundles` 里加上 `"dsh-notify"`。

装完重启 DSH，设置里会出现「完成提醒」。

## 使用

设置 → 完成提醒：

| 项 | 说明 |
|---|---|
| 启用 | 关闭后插件不再启动提醒进程 |
| 标题 / 副标题 | 卡片上的两行文字 |
| 宽度 / 高度 | 卡片尺寸（逻辑像素） |
| 位置 | 右下 / 左下 / 右上 / 左上 |
| 边距 X / Y | 距屏幕边缘的像素 |
| 插图 | 图片绝对路径 + 尺寸，显示在文字左侧 |
| 滑入 / 滑出 ms | 动画时长 |
| 测试 / 保存 | 立即预览 / 写入配置并重启进程 |

## 工作原理

- **宿主半侧** `lib/index.js`：Cordis 插件，`apply(ctx)` 里 spawn 提醒进程，并注册 `/dsh-notify` 路由（读配置 / 写配置 / 测试）
- **浏览器半侧** `lib/client.js`：通过 `window.__ModuleLoader__.load` 注册到 `settings.section` 插槽，渲染上面那张设置面板
- **提醒进程** `assets/dsh-notify.exe`：C# WinForms，监视 `~/.dsh/sessions` 的写入，停手 3 秒后弹卡片

## 构建 exe

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe 
  /win32manifest:assets\dsh-notify.manifest 
  /out:assets\dsh-notify.exe 
  /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs
```

源码见 `src/Program.cs`。

## License

MIT
## 退场特效

| `exit_fx` | 表现 |
|---|---|
| `auto` | 靠近屏幕边缘 → 滑出；否则走粒子特效 |
| `slide` | 直接滑出屏幕外 |
| `dissolve` | 灰飞烟灭：从点击位置起波前，方块逐块化灰、飞散、缩小、淡出 |
| `rise` | 飞升：屏幕顶部垂下光柱，弹窗被压扁 → 拉成竖条 → 被吸走 |
| `collapse` | 倒塌：几道裂纹 → 颤动 → 左右两片下坠，伴随细灰 |
| `hand` | 恶魔手：巨手从洞口升起，攥碎弹窗，碎片从手心向外溃散 |
| `random` | 每次随机抽一个上面的特效，每种概率相同；以后新增特效会自动进池，不用改这里 |

点右上角 ✕ 关闭时，碎裂从 ✕ 的位置开始；点卡片其它位置则从点击处开始。

所有退场特效的时长由 `fx_ms` 统一控制（默认 800ms）。

## 关于《以撒》素材

`rise` 的光柱与 `hand` 的恶魔爪原型取自《The Binding of Isaac: Rebirth》的解包资源，版权归 Edmund McMillen / Nicalis 所有，**不在本仓库中**。

仓库里的 `assets/dsh-notify.exe` 是不含这些素材的干净构建，缺素材时自动降级：

- `rise`：保留压扁 + 拉长吸走的动作，只是没有光柱
- `hand`：保留手心起碎 + 溃散，只是没有那只手

本仓库不包含、也不提供获取游戏美术资源的功能。

## 更新日志

### 0.7.10
- 恢复两条拍碎通道：停留过半的自动拍碎、以及鼠标拍地/闲置自动拍的冲击波拍碎；两者都只在拍地插件运行中才生效，没运行就完全按选定特效退场。

### 0.7.9
- 点 X 只播选定的退场特效，不再触发拍碎；弹窗被拍地打碎由拍地插件在真的有拍击（鼠标拍地 / 闲置自动拍）时接管，且碎成粉末。

### 0.7.8
- 拍碎改为底层常驻：不管 `exit_fx` 选哪种，退场时都会同时触发一次拍碎（装有拍地插件时生效，没有则静默跳过）。

### 0.7.7
- 旧配置里的 `exit_fx=wave` 自动落回 `auto`（拍碎已改为被动，不再作为可选项）。

### 0.7.5（未发布）
- 修复：退场特效还没播完进程就退出，动画被截断；现在等特效播完再退出。
- `exit_fx=random` 的随机池只包含界面上的五种特效。

### 0.7.4
- 修复：0.7.2 引入的单实例锁把 `--test` 也挡住了，导致界面上点「测试」没有弹窗。现在只有常驻 watcher 受锁限制。

### 0.7.3
- 修复：每次弹提示都会留下一个 `--show` 进程（实测堆到 8 个）；提示隐藏后进程自动退出。

### 0.7.2
- 修复：DSH 每次重载插件都会再拉起一个 helper，进程越堆越多（实测堆到 18 个）；helper 现在带单实例锁，只保留一个。
