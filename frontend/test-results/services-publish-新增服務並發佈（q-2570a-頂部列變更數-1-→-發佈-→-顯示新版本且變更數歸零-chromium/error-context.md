# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: services-publish.spec.ts >> 新增服務並發佈（quickstart §4，mock 資料來源） >> 新增服務 → 頂部列變更數 1 → 發佈 → 顯示新版本且變更數歸零
- Location: tests\e2e\services-publish.spec.ts:9:3

# Error details

```
Error: browserType.launch: Executable doesn't exist at C:\Users\e4913\AppData\Local\ms-playwright\chromium_headless_shell-1248\chrome-headless-shell-win64\chrome-headless-shell.exe
╔════════════════════════════════════════════════════════════╗
║ Looks like Playwright was just installed or updated.       ║
║ Please run the following command to download new browsers: ║
║                                                            ║
║     npx playwright install                                 ║
║                                                            ║
║ <3 Playwright Team                                         ║
╚════════════════════════════════════════════════════════════╝
```