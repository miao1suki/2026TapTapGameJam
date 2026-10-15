package com.miao1suki.shapeofcolor.privacy;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Bundle;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.view.Gravity;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

/** Native-only consent screen; no engine/library initialization or automatic web requests. */
public final class PrivacyActivity extends Activity {
    private Typeface font;
    private boolean launching;
    private boolean review;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        review = getIntent().getBooleanExtra("review", false) && PrivacyConsent.hasConsent(this);
        if (!review && PrivacyConsent.hasConsent(this)) { enterGame(); return; }
        font = Typeface.createFromAsset(getAssets(), "privacy-font.ttf");
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL); panel.setPadding(dp(24), dp(12), dp(24), dp(12));
        panel.setBackgroundColor(Color.rgb(245, 247, 250));
        TextView title = label("隐私政策与个人信息保护", 21); panel.addView(title);
        TextView introduction = label("请阅读《形之彩隐私政策》。同意后才会启动 Unity 引擎及游戏；不同意将退出游戏。未满14周岁的玩家请由监护人阅读并同意。", 15);
        panel.addView(introduction);
        ScrollView scroll = new ScrollView(this); scroll.setFillViewport(true);
        TextView policy = label("", 15); policy.setTextIsSelectable(true); policy.setMovementMethod(LinkMovementMethod.getInstance());
        boolean loaded = false;
        try { policy.setText(Html.fromHtml(PrivacyConsent.policy(this), Html.FROM_HTML_MODE_LEGACY)); loaded = true; }
        catch (Exception ignored) { policy.setText("隐私政策未能载入，请退出后重新安装。未经同意不会启动游戏。"); }
        scroll.addView(policy); panel.addView(scroll, new LinearLayout.LayoutParams(-1, 0, 1f));
        LinearLayout buttons = new LinearLayout(this); buttons.setGravity(Gravity.CENTER);
        Button online = button("查看在线政策"); online.setOnClickListener(view -> openOnline()); buttons.addView(online, equalWidth());
        if (review) {
            Button revoke = button("撤回同意并退出");
            revoke.setOnClickListener(view -> new android.app.AlertDialog.Builder(this)
                .setMessage("撤回后将停止游戏。下次进入需要重新阅读并同意隐私政策。")
                .setNegativeButton("取消", null).setPositiveButton("撤回并退出", (dialog, which) -> {
                    PrivacyConsent.withdrawConsentAndExit(this);
                }).show());
            buttons.addView(revoke, equalWidth());
            Button back = button("返回游戏"); back.setOnClickListener(view -> finish()); buttons.addView(back, equalWidth());
        } else {
            Button decline = button("不同意并退出"); decline.setOnClickListener(view -> finishAndRemoveTask()); buttons.addView(decline, equalWidth());
            Button agree = button("同意并进入游戏"); agree.setEnabled(loaded);
            agree.setOnClickListener(view -> {
                if (PrivacyConsent.accept(this)) enterGame();
                else Toast.makeText(this, "授权记录保存失败，请检查存储空间后重试", Toast.LENGTH_LONG).show();
            }); buttons.addView(agree, equalWidth());
        }
        panel.addView(buttons); setContentView(panel);
    }

    private void enterGame() {
        if (launching || !PrivacyConsent.hasConsent(this)) return;
        launching = true;
        try {
            startActivity(new Intent().setClassName(getPackageName(), "com.unity3d.player.UnityPlayerActivity")
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP));
            finishAndRemoveTask();
        } catch (ActivityNotFoundException error) {
            launching = false; Toast.makeText(this, "游戏入口不可用，请重新安装", Toast.LENGTH_LONG).show();
        }
    }

    private void openOnline() {
        try { startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(PrivacyConsent.POLICY_URL))); }
        catch (ActivityNotFoundException error) { Toast.makeText(this, "未找到浏览器，可直接阅读上方完整政策", Toast.LENGTH_LONG).show(); }
    }

    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private TextView label(String value, int size) {
        TextView text = new TextView(this); text.setTypeface(font); text.setText(value); text.setTextSize(size);
        text.setTextColor(Color.rgb(30, 39, 50)); text.setPadding(0, dp(4), 0, dp(4)); return text;
    }
    private Button button(String value) {
        Button button = new Button(this); button.setTypeface(font); button.setText(value); button.setTextSize(14);
        button.setAllCaps(false); button.setMinHeight(dp(48)); return button;
    }
    private LinearLayout.LayoutParams equalWidth() { return new LinearLayout.LayoutParams(0, -2, 1f); }
    @Override public void onBackPressed() { if (review) finish(); else finishAndRemoveTask(); }
}
