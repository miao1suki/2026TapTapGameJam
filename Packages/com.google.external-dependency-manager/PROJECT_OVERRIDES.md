# 项目内嵌配置

本包保留 EDM4U 1.2.179 原版 DLL、许可证和 Android 解析配置，仅禁用当前 Android/Windows 项目不使用的 iOS Resolver。

`ExternalDependencyManager/Editor/1.2.179/Google.IOSResolver.dll.meta`：

- Editor 兼容性关闭；Any Platform 原本即关闭。
- 去除 `gvhp_targets-editor` 标签，避免版本管理器按该标签重新启用。
- 增加未定义的 `EDM4U_ENABLE_IOS_RESOLVER` 导入约束，避免意外加载。

采用内嵌包是为了使该设置随项目保留，不直接修改会被重建的 Library/PackageCache。TapTap SDK 与 EDM4U 版本未升级；Google.JarResolver.dll 及其导入设置保持原样。

以后如正式支持 iOS，应先安装匹配 Unity 版本的 iOS Build Support，再显式恢复该 DLL 的 Editor 兼容性并移除上述导入约束。不要为了消除报错而关闭全局程序集引用校验。
