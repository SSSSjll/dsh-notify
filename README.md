# dsh-notify

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
dsh plugin --profile desktop add dsh-notify
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