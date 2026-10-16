# RunTime / Aqua.Net

這個 repo（`C:\專案\RunTime`，GitHub `g23722249-stack/Aqua.Net` 的 `main`）是 Aqua.Net 唯一的版本來源：

- `Aqua.Net\` — 控制項函式庫（net35 / net8.0-windows）
- `Aqua.Net.Demo\`、`Aqua.Net.Tests\`、`Tools\`、`WinFormsApp1\` — 示範、測試與工具
- `Aqua.Net.sln`

## 誰在用 Aqua.Net

以下方案以 `ProjectReference` 直接引用 `..\RunTime\Aqua.Net\Aqua.Net.vbproj`（共用同一份原始碼，不是副本）：

| 方案 | 位置 | 自己的 repo |
|---|---|---|
| 電子相簿（iPhoto.Net、PhotoLib.Net） | `C:\專案\iPhoto\電子相簿.Net.sln` | `g23722249-stack/iPhoto` |
| PhotoEdit | `C:\專案\PhotoEdit\PhotoEdit.sln` | `g23722249-stack/PhotoEdit` |
| PhotoMontage | `C:\專案\PhotoMontage\PhotoMontage.sln` | `g23722249-stack/PhotoMontage` |

## 規則

- **Aqua.Net 的修改只在這個 repo 提交。** 從其他方案的 session 改到 `Aqua.Net\` 時，也要到 `C:\專案\RunTime` 提交與推送，不要把 Aqua.Net 的檔案加進其他方案的 repo，也不要在 `Aqua.Net\` 底下建立另一個 `.git`。
- **往後相容。** 多個方案同時依賴它：新增成員優先，不要改名或刪除既有的公開屬性、事件、方法；行為變更要想到其他方案。
- **推送前建置全部使用者。** Aqua.Net（net35 與 net8.0-windows）、`Aqua.Net.Tests`，以及上表三個方案都要能建置。
- **net35 要用 Visual Studio 的 MSBuild 建置**（`dotnet build` 不支援 net35 的 resx/ResGen）。還原時不要只指定單一 `TargetFramework`，否則會蓋掉另一個框架的 `project.assets.json`。
- 提交前先 `git pull`，提交要小而頻繁，降低多個 session 互相覆蓋的機會。
