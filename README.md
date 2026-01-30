# FoundryProjects — Repo for Azure Foundry and Agent Framework Projects



This repository is a **monorepo** containing multiple .NET solutions for Azure AI Foundry and development using Agent Framework SDK. Each solution represents a focused QuickStart and is self-contained, while the `shared` folder provides reusable libraries and utilities.



This structure keeps the repo organized, scalable, and easy to maintain as more samples and QuickStarts are added over time.



---



## 🚀 Repository Structure



FoundryProjects/

├─ src/

│  ├─ QuickStart\_Chat\_With\_Agent/

│  │  ├─ QuickStart\_Chat\_With\_Agent.sln

│  │  ├─ projects/

│  │  └─ tests/

│  │

│  ├─ QuickStart-Chat-With-Model/

│  │  ├─ QuickStart-Chat-With-Model.sln

│  │  ├─ projects/

│  │  └─ tests/

│  │

│  ├─ QuickStart-Create-Agent/

│  │  ├─ QuickStart-Create-Agent.sln

│  │  ├─ projects/

│  │  └─ tests/

│

├─ shared/

│  └─ CommonLib/

│     ├─ CommonLib.csproj

│     └─ (shared helpers used across all solutions)

│

├─ docs/

│  ├─ architecture/

│  └─ notebooks/

│

├─ .github/

│  └─ workflows/

│     ├─ build-chat-with-agent.yml

│     ├─ build-chat-with-model.yml

│     └─ build-create-agent.yml

│

├─ .gitignore

└─ README.md



---



## 🧩 Solutions Included



### **1. QuickStart\_Chat\_With\_Agent**

Demonstrates:

- Chatting with Azure AI Foundry Agents  

- Handling messages and state  

- Integrating with Semantic Kernel or Azure REST SDKs  



Includes:

- A console app  

- An xUnit test project  



---

### **2. QuickStart-Chat-With-Model**

Demonstrates:

- Direct prompts to large language models  

- System and user prompt patterns  

- Token usage and cost estimation  

- Clean response handling  

Includes:

- Console app  

- xUnit tests  

---

### **3. QuickStart-Create-Agent**

Shows how to:

- Create Azure AI Foundry Agents  

- Configure function calling  

- Persist and manage agent workflows  

- Build multi-step orchestration patterns  

Includes:

- Console app  

- xUnit tests  

---

## 📦 Shared Code

The `/shared/CommonLib` library provides optional helpers used across multiple samples:

- DTOs  

- Logging and tracing helpers  

- Token accounting utilities  

- Configuration providers  

- HttpClient extensions  

- Semantic Kernel utilities  

## 🔄 GitHub Actions CI

Each solution has its own dedicated CI workflow, triggered only when files change in that solution's folder.

| Solution                    | Workflow File                   | Trigger Path                               |
|-----------------------------|----------------------------------|---------------------------------------------|
| QuickStart_Chat_With_Agent  | `build-chat-with-agent.yml`     | `src/QuickStart_Chat_With_Agent/**`         |
| QuickStart-Chat-With-Model  | `build-chat-with-model.yml`     | `src/QuickStart-Chat-With-Model/**`         |
| QuickStart-Create-Agent     | `build-create-agent.yml`        | `src/QuickStart-Create-Agent/**`            |

Each workflow performs the following steps:

- `dotnet restore`  
- `dotnet build` (targets .NET 8)  
- `dotnet test`  
- Publish test results to GitHub Actions  

This keeps all solutions **isolated, efficient, and fast to build**, while preserving a clean monorepo GitHub Actions structure.

## Reference the library in any project:
```bash
dotnet add <project.csproj> reference ../../shared/CommonLib/CommonLib.csproj
```

## 🧪 Testing
```bash
src/<SolutionName>/tests/
```
- Run tests for the entire repo:
```bash
dotnet test
```
- Run tests for a specific solution:
```bash
dotnet test src/QuickStart_Chat_With_Agent/QuickStart_Chat_With_Agent.sln
```

## 📘 Development Guidelines
- Prefer relative project references (never absolute paths)
- Keep each QuickStart self-contained
- Use the shared folder sparingly to avoid over-coupling
- Store diagrams in docs/architecture
- Store demos & Python notebooks in docs/notebooks
- Commit often and keep solutions clean and minimal
- When solutions get large, consider generating a .slnf (solution filter)

📄 License - MIT License
