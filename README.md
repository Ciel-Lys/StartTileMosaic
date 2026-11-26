# StartTileMosaic

为 Windows 11 上的 Windows 10 风格开始菜单制作图片磁贴。支持方形、宽幅、图片拼图和定时轮换。受微软商店版 TileGenie 启发，作为独立项目开发。

需要安装 [ExplorerPatcher](https://github.com/valinet/ExplorerPatcher) 并启用其 Windows 10 风格开始菜单。构建后双击 `dist\TileGenie.exe` 打开编辑器，使用 Windows 自带的 .NET Framework。

## 编辑与固定

1. 点击「添加图片」或将图片拖入窗口。支持 PNG、JPEG、BMP、GIF 和 TIFF。GIF 使用静态帧。
2. 用画布上方的圆形按钮增减列、切换方形与宽磁贴，用左侧按钮增减行。默认两行两列，左宽右方。
3. 在网格内拖动图片调整裁剪。按住 Ctrl 滚动滚轮可以缩放。多张图片可以分别裁剪，在左侧列表调整顺序并选择轮换间隔。选择「自定义」可输入 1 至 1440 分钟的整数。固定后会在后台安排轮换。
4. 在网址栏填写完整的 HTTP 或 HTTPS 地址，点击磁贴时会直接打开该网页。留空则打开 TileGenie 编辑器。请保留 `TileGenie.exe` 的位置，以便磁贴找到编辑器。
5. 点击「固定磁贴」，在开始屏幕按预览顺序排列。切片的显示名与悬停提示统一为 `z Custom Tile by TileGenie`，默认隐藏磁贴表面的标题。图片使用次级磁贴，不在「所有程序」中逐片添加应用入口。

通过左上角「语言 / Language」可以切换简体中文和 English。界面立即更新，重新打开编辑器时沿用上次选择。

左上角菜单可旋转图片、复位裁剪、设置间距和预览编号。如需取消固定，请在开始菜单中右键点击对应磁贴。

## 保存与安装

磁贴数据保存在 `%LOCALAPPDATA%\TileMosaic\Projects`，其中的 Package 文件夹供 Windows 读取。使用磁贴期间请保留该文件夹的位置。

本地磁贴包通过 `Add-AppxPackage -Register` 注册。若系统返回 0x80073CFF，请在 Windows 设置中启用开发人员模式，然后在编辑器中再次点击「固定磁贴」。轮换使用 Windows 动态磁贴通知，实际刷新时间可能受到系统节电和后台设置影响。

## 构建

在项目目录运行 `.\build.ps1` 构建编辑器。编辑器支持 `--library <文件夹>` 指定磁贴资源目录，以及 `--image <图片路径>` 导入图片。

## 许可证

项目采用 [MIT 许可证](LICENSE)。接口来源及其版权声明见[第三方声明](THIRD_PARTY_NOTICES.md)。
