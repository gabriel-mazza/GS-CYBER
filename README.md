# Deslocai
 
Transporte público mais previsível, confortável e acessível: o cidadão sabe antes de sair de casa se a estação está lotada, se está um calor insuportável na plataforma e se o elevador de que precisa está funcionando.
 
## Ficha da CitizenTech
 
| Campo | Resposta |
|---|---|
| **Nome da CitizenTech** | Deslocai |
| **Cidade e bairro** | São Paulo |
| **Cidadão beneficiado** | Trabalhadores diários, estudantes, idosos e pessoas com deficiência (PCD) que dependem de metrô, trem e ônibus em horários de pico |
| **Problema, em uma frase** | Passageiros do transporte público sofrem diariamente com superlotação imprevisível, calor extremo nos vagões/pontos e falta de avisos em tempo real sobre elevadores e escadas rolantes quebrados |
| **Como funciona** | Sensores ESP32 nas estações enviam temperatura, umidade e contagem de pessoas para esta API. A API aplica regras de negócio, gera alertas (superlotação, calor, equipamento parado) e expõe tudo para o aplicativo mobile e para os painéis |
| **O que cada parte faz** | **Backend C# (este repositório):** API REST, regras de alerta, banco e indicadores. **Cybersecurity (este repositório, `src/Deslocai.Api/Security`):** mapeamento de PII, Taint Analysis (modos Auditoria/Bloqueio) e tradução por IA para o cidadão, com o painel Risco Cidadão em `/dashboard.html`. _[grupo: completar com Mobile, Physical Computing, IA/ML, SOA, Operating Systems e Testing]_ |
| **Link do repositório** | https://github.com/gabriel-mazza/GS-CYBER.git |
| **Indicador de impacto** | Redução do tempo de espera em estações superlotadas (min) e redução de viagens perdidas por PCD/idosos por elevadores inoperantes |
| **ODS conectados** | ODS 9 (modernização da infraestrutura de transporte com IoT e dados), ODS 10 (mobilidade digna para PCD, idosos e trabalhadores periféricos), ODS 11 (transporte coletivo mais seguro, previsível e eficiente) |
 
**Integrantes:** Gabriel Barros Mazzariol - RM 555410, Jefferson Junior Alvarez Urbina - RM 558497
 
## O que o backend entrega
 
- **Estações** (metrô, trem, ônibus) com detalhe de acessibilidade, ocupação e temperatura atuais.
- **Equipamentos** (elevadores e escadas rolantes). Quando um equipamento fica indisponível, um alerta de acessibilidade é aberto automaticamente; quando volta a operar, é encerrado.
- **Leituras de sensor (ESP32):** cada leitura passa pelas regras de superlotação e calor extremo, gera alertas sem duplicar e encerra o alerta quando a situação normaliza.
- **Alertas** ordenados por gravidade.
- **Relatos colaborativos** dos passageiros, sem dados pessoais (se o passageiro digitar algum dado pessoal no texto, o módulo de Cybersecurity o mascara antes de gravar).
- **Indicadores de impacto:** % de equipamentos operantes, estações sem elevador funcionando, estações mais lotadas nas últimas 24 h.
> O tempo de espera real não é medido pelo protótipo. Usamos como proxy a ocupação das plataformas e a disponibilidade dos elevadores.
 
## Como rodar (Windows / PowerShell)
 
Pré-requisito: .NET SDK 8 ou superior.
 
```powershell
git clone https://github.com/gabriel-mazza/GS-CYBER.git
cd GS-CYBER
dotnet restore
dotnet run --project src/Deslocai.Api
```
 
A API sobe em `http://localhost:5080` e abre o Swagger (`/swagger`). O banco SQLite (`deslocai.db`) é criado na primeira execução, já com dados simulados de estações de São Paulo (coordenadas aproximadas e capacidades fictícias).
 
O painel de Cybersecurity (Risco Cidadão) fica em `http://localhost:5080/dashboard.html`.
 
### Testes
 
```powershell
dotnet test                                                  # 43 testes unitários do backend (xUnit)
dotnet run --project src/Deslocai.Api -- --selftest          # 55 testes do módulo de Cybersecurity
```
 
### Migrations do EF Core (opcional, recomendado para a entrega)
 
Sem migrations o banco é criado direto do modelo. Para versionar o schema:
 
```powershell
dotnet tool install --global dotnet-ef
Remove-Item src/Deslocai.Api/deslocai.db -ErrorAction SilentlyContinue
dotnet ef migrations add Inicial --project src/Deslocai.Api
dotnet run --project src/Deslocai.Api
```
 
Com a pasta `Migrations` presente, a API passa a aplicar `Migrate()` na inicialização automaticamente.
 
## Endpoints
 
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/estacoes?tipo=&linha=` | Lista estações (filtros opcionais) |
| GET | `/api/estacoes/{id}` | Detalhe: acessível para PCD?, ocupação, temperatura, alertas ativos |
| POST / PUT / DELETE | `/api/estacoes`, `/api/estacoes/{id}` | CRUD de estações |
| GET | `/api/equipamentos?estacaoId=&status=` | Lista elevadores/escadas rolantes |
| POST | `/api/equipamentos` | Cadastra equipamento |
| PATCH | `/api/equipamentos/{id}/status` | Altera status e abre/encerra o alerta |
| POST | `/api/leituras` | Recebe leitura do ESP32 e devolve os alertas gerados |
| GET | `/api/estacoes/{id}/leituras?quantidade=10` | Últimas leituras |
| GET | `/api/alertas?estacaoId=&tipo=&incluirResolvidos=` | Alertas, do mais grave ao menos grave |
| PATCH | `/api/alertas/{id}/resolver` | Resolve um alerta |
| GET / POST | `/api/relatos` | Relatos dos passageiros (PII na descrição é mascarada) |
| GET | `/api/indicadores/impacto` | Indicadores de impacto |
 
Erros seguem o padrão ProblemDetails: 400 (validação/regra de negócio), 404 (não encontrado), 409 (duplicidade).
 
### Endpoints de Cybersecurity
 
| Método | Rota | Descrição |
|---|---|---|
| GET | `/dashboard.html` | Painel executivo Risco Cidadão (farol verde/amarelo/vermelho, indicador 0–100, fluxos source → sink) |
| POST | `/api/pii/scan` | Motor de parsing: detecta, classifica (LGPD) e mascara PII de um texto |
| GET | `/api/findings` | Fluxos rastreados pelo Taint Analysis (evidências sempre mascaradas) |
| GET | `/api/risk` | Indicador Risco Cidadão (0–100), por endpoint e evolução |
| GET | `/api/citizen` | Farol e mensagem ao cidadão traduzida por IA |
| GET / POST | `/api/mode`, `/api/mode/{auditoria\|bloqueio}` | Alterna o modo do Taint: Auditoria (registra) ou Bloqueio (impede o vazamento) |
| POST | `/api/remediation/{id}` | Remediação assistida por IA (explicação, LGPD, patch em C#) |
| POST | `/api/v1/relatos` | Endpoint **propositalmente vulnerável**, usado só na demonstração ("antes") |
| POST | `/api/v2/relatos` | Mesma operação, corrigida ("depois") |
 
### Exemplo: leitura do ESP32
 
```powershell
$corpo = @{ estacaoId = 1; temperaturaC = 37; umidadePercentual = 90; pessoasNaPlataforma = 3300 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/leituras -ContentType "application/json" -Body $corpo
```
 
No Wokwi, o ESP32 faz o mesmo POST com `HTTPClient` enviando esse JSON.
 
## Cybersecurity (Global Solution)
 
Módulo em `src/Deslocai.Api/Security`, plugado no `Program.cs` com `AddDeslocaSecurity()` e `UseDeslocaSecurity()`:
 
1. **Mapeamento de PII (motor de parsing):** middleware que lê query, headers e JSON de toda requisição; regex + validação (dígito verificador de CPF/CNPJ, Luhn) para 14 tipos de dado; mascaramento (`***.***.***-25`); senha detectada pelo nome da chave; laudo/CID e PCD tratados como dado sensível (LGPD art. 5º, II).
2. **Taint Analysis:** rastreia o dado da entrada (body, query, headers) até 5 saídas: log, banco (SQL), HTTP externo, arquivo e resposta HTTP. **Modo Auditoria** registra o vazamento; **modo Bloqueio** redige o log e a resposta e impede o SQL/HTTP com PII.
3. **Tradução via IA (Risco Cidadão):** o evento técnico vai a um LLM (Claude), que o transforma em 2 frases para um cidadão leigo de 60 anos; o painel mostra o farol e a mensagem. Sem `ANTHROPIC_API_KEY` (ou se a IA falhar), usa mensagens offline.
```powershell
# IA real (opcional)
$env:ANTHROPIC_API_KEY = "sk-ant-..."
# Modo do Taint: botão no painel, ou em appsettings.json -> Seguranca:ModoTaint = "Auditoria" | "Bloqueio"
```
 
Roteiro de demonstração no painel: modo Auditoria + **Simular tráfego VULNERÁVEL (v1)** (farol vermelho) → **Remediar com IA** → modo Bloqueio + v1 (HTTP 422 com mensagem humana, farol amarelo) → **Reset** + **Simular tráfego CORRIGIDO (v2)** (farol verde, risco 0).
 
O relato real (`POST /api/relatos`) passa a descrição por um sanitizer antes de gravar: `"Elevador parado, meu CPF 529.982.247-25"` é gravado como `"Elevador parado, meu CPF ***.***.***-25"`. A proteção pode ser desligada para demonstração com `Seguranca:MascararPiiEmRelatos = false` no `appsettings.json`.
 
Limitações: taint dinâmico (só os pontos de saída instrumentados pelo `SinkGuard`); detecção por regex + validação (nomes e endereços completos não são detectados); eventos do painel ficam em memória; relatos antigos já gravados não são reescritos.
 
## Arquitetura
 
```
src/Deslocai.Api
├── Domain           Entidades, enums, exceções, IRepositorio e regras de alerta (IRegraAlerta)
├── Application      DTOs, interfaces e serviços (regras de aplicação)
├── Infrastructure   DbContext (EF Core + SQLite), repositório genérico, carga inicial
├── Controllers      Endpoints REST
├── Middleware       Tratamento global de exceções
├── Security         Cybersecurity: Pii (parsing), Taint (source → sink), Remediation (IA), Risk (Risco Cidadão), Demo, Tests
└── wwwroot          Painel Risco Cidadão (dashboard.html)
tests/Deslocai.Tests Testes unitários (xUnit)
```

<img width="1906" height="523" alt="image" src="https://github.com/user-attachments/assets/2fa09e41-2d1a-4d35-9a38-23e210be5644" />

<img width="1920" height="1022" alt="image" src="https://github.com/user-attachments/assets/7376f18d-f0a1-4d48-a9c7-59fa58ce919a" />


 
## Onde cada conceito da disciplina aparece
 
| Critério | Onde ver no código |
|---|---|
| POO (3,0) | Herança: `EntidadeBase`. Encapsulamento: setters privados e validação nos construtores (`Estacao`, `LeituraSensor`...). Polimorfismo e interfaces: `IRegraAlerta` com `RegraSuperlotacao` e `RegraCalorExtremo`. Exceções de domínio em `Domain/Excecoes.cs` |
| Injeção de Dependência | `Program.cs` registra repositório genérico, regras e serviços; tudo é recebido por construtor |
| Web API (2,5) | `Controllers/`, DTOs com validação, verbos HTTP e status corretos, ProblemDetails, Swagger com comentários XML |
| EF Core + LINQ (1,5) | `DeslocaiDbContext` (relações, índices únicos, cascata, conversão de enums). LINQ em `Application/*Service.cs`: filtros, GroupBy, Distinct, Include, projeções, agregações |
| Async/Await (1,0) | Toda a cadeia (controller → serviço → repositório) é assíncrona, com `CancellationToken` |
| Qualidade do código (2,0) | Camadas separadas, DTOs (a entidade nunca vaza), nomes claros, testes unitários |
| Testes unitários | `tests/Deslocai.Tests` (domínio, regras e serviços com EF InMemory) |
| Cybersecurity | `src/Deslocai.Api/Security` (PII, Taint Analysis, IA, painel) e `-- --selftest` |
 
## Dados e privacidade
 
Todos os dados são **simulados**. Relatos guardam só o perfil genérico (idoso, PCD...) e um texto curto; se o passageiro digitar dados pessoais nesse texto (CPF, telefone, e-mail...), o módulo de Cybersecurity os mascara antes de gravar.
 
