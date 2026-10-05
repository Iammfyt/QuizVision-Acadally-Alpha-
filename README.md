# QuizVision

AI-powered visual teaching assistant built with C# and WPF.

## Features

* 🖥️ Select a window to scan
* 📸 Capture the selected window
* 🤖 Analyze the screenshot with Gemini
* 💡 Get a suggested answer
* 📚 Get a short explanation
* 🌙 Dark futuristic UI

## Requirements

* Windows
* .NET SDK/runtime required by the project
* A Gemini API key

## Getting Started

### 1. Get your own Gemini API key

QuizVision does **not** include an API key.

Create your own Gemini API key through Google AI Studio.

Never use or share someone else's API key.

### 2. Add your API key using CMD

Open **Command Prompt** and run:

```cmd
setx GEMINI_API_KEY "YOUR_API_KEY_HERE"
```

Replace `YOUR_API_KEY_HERE` with your own Gemini API key.

For example:

```cmd
setx GEMINI_API_KEY "YOUR_API_KEY_HERE"
```

> Do not paste your real API key into this README, GitHub, screenshots, issues, or other public places.

After running the command, **close and reopen Visual Studio** so it can see the new environment variable.

### 3. Run QuizVision

Open the project in Visual Studio and build/run it.

QuizVision reads your API key from:

```text
GEMINI_API_KEY
```

The key is stored as a Windows user environment variable rather than inside the source code.

## Security

**Never hardcode your Gemini API key into the project.**

Do not commit API keys to GitHub.

If you accidentally expose a key, replace/revoke it and update your environment variable with the new key.

## Disclaimer

QuizVision is an educational/experimental project designed to demonstrate screenshot-based AI assistance.

Use AI-generated answers responsibly and verify important information yourself.

## License

Add your preferred license here.
