# 形之彩隐私政策静态页

公开地址：<https://meowgame.cloud/shape-of-color/privacy/>。

唯一正文为 `Assets/_Project/Resources/Privacy/privacy-policy.txt`；不要另写或手动修改网站条款。原生同意版本仍只由这份正文的 SHA-256 决定，改网页样式不让已同意玩家重新授权。

## 生成与验证

```powershell
node --test Ops/PrivacyPolicy/build-site.test.mjs
node Ops/PrivacyPolicy/build-site.mjs
```

输出在忽略目录 `Library/PrivacyWebsite/index.html`。生成器从原生 `PrivacyConsent.POLICY_URL` 读取地址，把正文逐字放进模板；摘要写在页面元数据，用户只看到政策与备案。模板包含响应式中文排版、可横向滚动的表格及印刷样式，无脚本、统计、外部字体、图片、登录、导航或其他产品内容。

备案信息沿用公开主页的“豫ICP备2026018798号”，链接工信部备案系统；没有已确认的公安备案号，不伪造或猜测添加。主体、联系方式、SDK 等法律条款保持既有内容，发布负责人仍须核对与实际行为相符。

## 部署与维护

通过获准的部署连接上传生成文件至现有 HTTPS Nginx 静态根的 `shape-of-color/privacy/index.html`。不修改网站主页、Nginx 代理路由、机器人、唤醒服务或 TLS 配置，不需要重启服务。更新前将旧页备份至 Web 根目录以外，发布后通过公开 HTTPS 请求比对完整文件及正文摘要，再检查桌面、手机竖屏与横屏。

SSH 私钥只在用户授权的仓库外位置使用，不复制进项目、日志、生成物或文档。无需云平台 API Key。开发机部署不代表游戏联网：安卓仍无 INTERNET/局域网权限，网页只由用户主动点击后交系统浏览器加载，原生离线阅读和授权边界保留。

旧 APK 不会自动更新。原生代码修改后需要后续重新打包，并在真机验收：未同意前不启动 Unity、不同意退出、主动打开网页、浏览器返回、不因浏览而同意、断网离线阅读、无浏览器回退、设置查看/撤回与最近任务恢复。
