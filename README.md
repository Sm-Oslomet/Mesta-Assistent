# Mesta AI Assistant (No Database)

This is a simplified version of the original project that **does not use any database**.

- **Retrieval (RAG):** Azure AI Search (vector search)
- **Generation:** Azure OpenAI (chat completions)
- **Chat history:** optional, **client-managed** (send last N messages in each request)

## What you need to configure
Edit `src/AiAssistant.Api/appsettings.json`:

- `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`
- `AzureOpenAI:ChatDeployment`, `AzureOpenAI:EmbeddingDeployment`
- `AzureSearch:Endpoint`, `AzureSearch:ApiKey`, `AzureSearch:IndexName`
- Field names for your index (defaults are common):
  - `ContentField`, `TitleField`, `UrlField`, `VectorField`

> This API uses REST calls, so you don't need extra Azure SDK packages.

## Run
From the solution folder:

```bash
cd src/AiAssistant.Api
# with .NET 8 installed
dotnet restore
dotnet run
```

Swagger (in Development): `https://localhost:5001/swagger`

## API
### POST `/api/chat`
Example request (single turn):

```json
{
  "question": "What is the return policy?"
}
```

Example request (multi-turn without a DB):

```json
{
  "question": "Can you summarize it in 2 bullets?",
  "messages": [
    { "role": "user", "content": "What is the return policy?" },
    { "role": "assistant", "content": "..." }
  ],
  "topK": 6
}
```

Response:
- `answer`: model answer with citations like `[S1]`
- `sources`: the retrieved snippets used to answer
