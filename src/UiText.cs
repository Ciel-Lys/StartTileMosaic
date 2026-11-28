using System;
using System.Collections.Generic;
using System.IO;

namespace TileMosaic
{
    internal static class UiText
    {
        public static bool English { get; private set; }
        static string preferencePath;
        static readonly Dictionary<string, string> translations = new Dictionary<string, string>
        {
            { "菜单", "Menu" },
            { "添加图片…", "Add Images…" },
            { "固定磁贴", "Pin Tiles" },
            { "帮助", "Help" },
            { "全局绑定（可选）", "Global binding (optional)" },
            { "绑定 · 第 {0} 行第 {1} 列", "Binding · Row {0}, Column {1}" },
            { "返回全局绑定", "Back to global binding" },
            { "网址", "URL" },
            { "选择文件…", "Choose File…" },
            { "清除", "Clear" },
            { "选择要打开的文件", "Choose the file to open" },
            { "所有文件", "All files" },
            { "请选择有效的文件。", "Choose a valid file." },
            { "磁贴绑定类型无效。", "Invalid tile binding type." },
            { "输入以 http:// 或 https:// 开头的网址。", "Enter a URL starting with http:// or https://." },
            { "编辑网址绑定", "Edit URL binding" },
            { "为这块磁贴选择文件。", "Choose a file for this tile." },
            { "为全部磁贴选择文件，并覆盖单块绑定。", "Choose a file for all tiles and replace individual bindings." },
            { "清除单块绑定并沿用全局设置。", "Clear this tile's binding and use the global binding." },
            { "清除全部磁贴的绑定。", "Clear the bindings for all tiles." },
            { "图片轮换", "Cycle images" },
            { "图片轮换间隔", "Image rotation interval" },
            { "自定义", "Custom" },
            { "分钟", "min" },
            { "自定义轮换间隔（分钟）", "Custom rotation interval in minutes" },
            { "轮换图片列表", "Images in rotation" },
            { "移除", "Remove" },
            { "上移图片", "Move image up" },
            { "下移图片", "Move image down" },
            { "磁贴画布", "Tile canvas" },
            { "添加一列", "Add a column" },
            { "添加一行", "Add a row" },
            { "删除第 {0} 列", "Remove column {0}" },
            { "删除第 {0} 行", "Remove row {0}" },
            { "将第 {0} 列切换为方形", "Make column {0} square" },
            { "将第 {0} 列切换为宽磁贴", "Make column {0} wide" },
            { "将画布切换为大磁贴（310 × 310）", "Switch the canvas to large tiles (310 × 310)" },
            { "退出大磁贴模式", "Exit large tile mode" },
            { "行列布局", "Grid Layout" },
            { "自由组合", "Free Layout" },
            { "大磁贴", "Large Tiles" },
            { "添加磁贴 ▾", "Add Tile ▾" },
            { "中（150 × 150）", "Medium (150 × 150)" },
            { "宽（310 × 150）", "Wide (310 × 150)" },
            { "大（310 × 310）", "Large (310 × 310)" },
            { "长按左键后拖动 · 右键删除", "Hold, then drag · Right-click to remove" },
            { "选择「添加磁贴」开始", "Select Add Tile to begin" },
            { "自由组合中，点击「添加磁贴」选择尺寸。长按磁贴直到光标变为移动图标，再拖到空位。右键单击可删除磁贴。", "In Free Layout, choose a size from Add Tile. Hold the left button on a tile until the move cursor appears, then drag it into an empty space. Right-click a tile to remove it." },
            { "绑定 · 第 {0} 块磁贴", "Binding · Tile {0}" },
            { "自由组合布局无效。", "Invalid free tile layout." },
            { "画布没有足够的空间。", "There is not enough space on the canvas." },
            { "开始菜单的磁贴组太窄，无法放入所选尺寸的磁贴。", "The Start tile group is too narrow for the chosen tile size." },
            { "第 {0} 块磁贴的网址必须以 http:// 或 https:// 开头。", "The website for tile {0} must start with http:// or https://." },
            { "已按自由组合布局固定 {0} 块磁贴。", "Pinned {0} tile(s) using the free layout." },
            { "已固定 {0} 块磁贴，分为 {1} 组。可在开始菜单调整各组位置。", "Pinned {0} tiles in {1} groups. Rearrange the groups on Start." },
            { "已固定 {0} 块磁贴（{1} 组），每 {2} 分钟轮换图片。", "Pinned {0} tiles in {1} groups. Images change every {2} minutes." },
            { "5 分钟", "5 minutes" },
            { "10 分钟", "10 minutes" },
            { "15 分钟", "15 minutes" },
            { "30 分钟", "30 minutes" },
            { "1 小时", "1 hour" },
            { "3 小时", "3 hours" },
            { "6 小时", "6 hours" },
            { "12 小时", "12 hours" },
            { "24 小时", "24 hours" },
            { "画布选项…", "Canvas Options…" },
            { "放大图片", "Zoom In" },
            { "缩小图片", "Zoom Out" },
            { "复位裁剪", "Reset Crop" },
            { "顺时针旋转", "Rotate Clockwise" },
            { "画布选项", "Canvas Options" },
            { "预览行列编号", "Show grid coordinates" },
            { "在磁贴上显示标题", "Show titles on tiles" },
            { "间距补偿", "Tile gap" },
            { "确定", "OK" },
            { "操作帮助", "Help" },
            { "添加图片后，拖动画布中的图片裁剪，按住 Ctrl 并滚动滚轮缩放。", "Add an image and drag it on the canvas to crop it. Hold Ctrl and scroll to zoom." },
            { "行列布局中，列按钮位于画布上方，行按钮位于左侧。点击「大磁贴」可切换整个画布。", "In Grid Layout, use the controls above the canvas for columns and the controls on the left for rows. Large Tiles switches the whole canvas." },
            { "选中图片可单独裁剪、移除或调整顺序。多张图片可设置轮换。", "Select an image to crop, remove or reorder it. Add multiple images to enable rotation." },
            { "磁贴可绑定网址或文件。选中单块可单独更改，全局绑定会覆盖单块设置。", "Bind a tile to a URL or a file. Select a tile to change its binding. A global binding replaces individual bindings." },
            { "图片", "Images" },
            { "最多支持 20 张图片。", "You can add up to 20 images." },
            { "部分图片未能打开", "Some images could not be opened" },
            { "拼图", "Mosaic" },
            { "检查输入", "Check your settings" },
            { "正在裁剪图片并准备磁贴…", "Cropping images and preparing tiles…" },
            { "正在安装图片资源…", "Installing tile images…" },
            { "正在固定磁贴…", "Pinning tiles…" },
            { "正在固定磁贴，请等待完成后再关闭。", "Tiles are being pinned. Please wait before closing this window." },
            { "已固定 {0} 块磁贴。到开始菜单按行列排列即可。", "Pinned {0} tile(s). Arrange them on Start to match the preview." },
            { "磁贴已固定，正在安排图片轮换…", "Tiles pinned. Scheduling image rotation…" },
            { "已固定 {0} 块磁贴，每 {1} 分钟轮换图片。", "Pinned {0} tiles. Images change every {1} minutes." },
            { "磁贴已固定，已安排未来 96 次轮换。点击磁贴可续排。", "Tiles pinned. The next 96 changes are scheduled. Click a tile to renew." },
            { "固定未完成。请根据提示处理后再试。", "Pinning did not finish. Check the details and try again." },
            { "磁贴已固定。点击任一磁贴可重试初始化图片轮换。", "Tiles pinned. Click a tile to try setting up image rotation again." },
            { "正在准备本机签名证书…", "Preparing a local signing certificate…" },
            { "无法读取本机签名证书。", "Could not read the local signing certificate." },
            { "无法信任本机签名证书。请检查管理员权限。", "Could not trust the local signing certificate. Check administrator permissions." },
            { "首次固定需要信任本机生成的磁贴签名证书。Windows 会请求管理员授权，将证书加入本机的“受信任的人”。\n\n指纹：{0}\n\n是否继续？",
                "Pinning for the first time requires trusting the signing certificate generated on this PC. Windows will ask for administrator approval to add it to Trusted People on this PC.\n\nThumbprint: {0}\n\nContinue?" },
            { "无法保存语言偏好。", "Could not save the language preference." },
            { " 宽磁贴", " wide tiles" },
            { " 方形磁贴", " square tiles" },
            { " 大磁贴", " large tiles" },
            { "行列或间距超出支持范围。", "The grid dimensions or spacing are outside the supported range." },
            { "列宽无效。", "Invalid column width." },
            { "网址必须以 http:// 或 https:// 开头。", "Enter a website starting with http:// or https://." },
            { "磁贴绑定位置超出画布。", "A binding is assigned to a tile outside the grid." },
            { "第 {0} 行第 {1} 列的网址必须以 http:// 或 https:// 开头。", "The website for row {0}, column {1} must start with http:// or https://." },
            { "支持最多 20 张轮换图片，间隔为 {0} 至 {1} 分钟。", "Choose up to 20 images and a rotation interval of {0} to {1} minutes." },
            { "图片裁剪参数无效。", "Invalid image crop." },
            { "作品名称需要 1–32 个字符。", "The project name must contain 1 to 32 characters." },
            { "裁剪参数无效。", "Invalid crop settings." },
            { "作品 ID 无效。", "Invalid project ID." },
            { "不是磁贴工坊作品。", "This file is not a TileGenie project." },
            { "图片超过 1.2 亿像素，请先缩小图片。", "Resize images larger than 120 megapixels before adding them." },
            { "作品与磁贴包的标识不一致。", "The tile package does not match this project." },
            { "磁贴缺少显示配置。", "The tile is missing its display settings." },
            { "请先添加图片。", "Add an image first." },
            { "图片与裁剪参数数量不一致。", "The image and crop counts do not match." },
            { "开始菜单保存超时，请稍后再试。", "Start took too long to save. Try again in a moment." },
            { "作品身份与已安装应用不匹配。", "The installed app does not match this project." },
            { "开始菜单布局受系统策略限制，未更改任何磁贴。", "System policy prevents changes to the Start layout." },
            { "开始菜单未接受磁贴 {0}。", "Start could not pin tile {0}." },
            { "安装后的包不存在。", "The installed package could not be found." },
            { "正在准备图片…", "Preparing images…" },
            { "下一张预览", "Next image" },
            { "打开网址", "Open website" },
            { "关闭", "Close" },
            { "系统没有打开网址，请检查默认浏览器。", "Check your default browser and try opening the website again." },
            { "网址打开失败：", "Could not open the website: " },
            { "已计划轮换：每 {0} 分钟切换，正在配置后台续期…", "Images will change every {0} minutes. Setting up background refresh…" },
            { "图片磁贴已准备好。", "Your picture tiles are ready." },
            { "轮换已开启：每 {0} 分钟切换。关闭窗口后继续轮换。\n系统节电或后台限制可能延迟刷新；再次打开磁贴会自动续期。", "Images will change every {0} minutes, even after you close this window.\nPower-saving settings may delay updates. Open a tile again to renew the schedule." },
            { "图片轮换已计划未来 96 次；后台续期未获准，请定期打开磁贴续期。\n", "The next 96 changes are scheduled. Background access was denied. Open a tile periodically to renew the schedule.\n" },
            { "图片轮换已计划未来 96 次；后台续期未能启用。\n", "The next 96 changes are scheduled. Background refresh could not be enabled.\n" },
            { "磁贴更新失败：", "Could not update the tiles: " },
        };

        public static string Get(string key)
        {
            string value;
            return English && translations.TryGetValue(key, out value) ? value : key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static void Load(string path)
        {
            preferencePath = path;
            English = false;
            try { English = File.ReadAllText(path).Trim() == "en"; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static void Select(bool english)
        {
            English = english;
            if (preferencePath == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(preferencePath));
            string temp = preferencePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, english ? "en" : "zh-CN");
                if (File.Exists(preferencePath)) File.Replace(temp, preferencePath, null);
                else File.Move(temp, preferencePath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
