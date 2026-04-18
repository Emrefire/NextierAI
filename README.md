# 🧠 NexTierAI - Local AI Desktop Solution

NexTierAI is a high-performance Windows desktop application built with **WinUI 3**, designed to provide a private, fast, and local Artificial Intelligence experience. By leveraging the **Clean Architecture** principles and **Local LLM** integration, it ensures user data stays on-device while providing advanced AI capabilities.

![WinUI 3](https://img.shields.io/badge/UI-WinUI_3-blue?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![LLM](https://img.shields.io/badge/LLM-Qwen_2.5_(3B)-orange?style=for-the-badge&logo=ollama)
![Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-green?style=for-the-badge)

## 🚀 Key Features

- **Local LLM Integration:** Powered by **Ollama**, running the **Qwen 2.5 (3B)** model locally. No API keys, no data leaks, and zero latency from external servers.
- **RAG (Retrieval-Augmented Generation) Ready:** Built-in infrastructure for vector search and document processing to provide context-aware responses.
- **Privacy First:** All chat histories and vector embeddings are stored locally, making it an ideal solution for sensitive data.
- **Modern Windows UI:** Built with **WinUI 3 (Windows App SDK)** for a native, sleek, and high-performance Windows 11 experience.

## 🏗️ Technical Architecture (Clean Architecture)

The project is structured into four layers to ensure scalability and maintainability:

- **NexTierAI.Domain:** Contains core entities, interfaces, and business logic.
- **NexTierAI.Application:** Implements use cases, orchestrators (like MentorOrchestrator), and service logic.
- **NexTierAI.Infrastructure:** Handles external integrations such as **Ollama API** and local Vector Database services.
- **NexTierAI.UI:** The presentation layer built with WinUI 3, providing an intuitive interface for users.

## 🛠️ Tech Stack

- **Frontend:** WinUI 3, XAML
- **Backend:** .NET 8, C#
- **AI Engine:** Ollama (Qwen 2.5 - 3B model)
- **Patterns:** Dependency Injection, Repository Pattern, Interface-based design.
