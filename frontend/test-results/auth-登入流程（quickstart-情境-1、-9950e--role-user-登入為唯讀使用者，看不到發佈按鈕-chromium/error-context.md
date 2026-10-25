# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: auth.spec.ts >> 登入流程（quickstart 情境 1、2、3、9、10、11、12） >> 以 role=user 登入為唯讀使用者，看不到發佈按鈕
- Location: tests\e2e\auth.spec.ts:72:3

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