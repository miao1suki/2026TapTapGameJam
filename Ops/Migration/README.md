# 原生 2D 资源检查

此目录仅保留可重复执行的只读检查，不在打开 Scene 或构建时执行一次性迁移。

```powershell
python Ops/Migration/audit_native_2d.py
python Ops/Migration/audit_native_2d.py --compare-ref 9a68c64
```

需要 Python 3.10+ 和 Git，无第三方包。`--compare-ref` 应指定本地存在的迁移前提交；额外核对 Scene 原 Transform XY、GridArt 作者资产和历史 JSON。退出码 0 表示通过；错误项列在 JSON 输出中。它不替代 Unity 编译、物理接触或视觉验收。

Unity 专项入口是 `Project.Migration.PlanarMigrationAudit.Run`，显式限制为 `Library/Native2DValidation` 内的隔离工程；检查真实 Scene、原生类型、正交相机、星空预览不标脏，以及实际 GPU 截图。需要图形设备，不能加 `-nographics`。`BuildWindowsSmoke` 仅生成隔离 Windows 验证包，不是正式发布构建。

迁移前资源快照与已执行的一次性工具位于本机忽略的 `Library/PlanarMigration`，不纳入交付。实施、验收及限制见 [迁移报告](../../Docs/Developer/NATIVE_2D_MIGRATION_20261105.md)。
