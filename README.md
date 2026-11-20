# Deslocai

> Transporte público mais previsível, confortável e acessível: o cidadão sabe **antes de sair de casa** se a estação está lotada, se está um calor insuportável na plataforma e se o elevador de que precisa está funcionando.

## Ficha da CitizenTech

| Campo | Resposta |
|---|---|
| **Nome da CitizenTech** | Deslocai |
| **Cidade e bairro** | _[preencher: ex. São Paulo – região central]_ |
| **Cidadão beneficiado** | Trabalhadores diários, estudantes, idosos e pessoas com deficiência (PCD) que dependem de metrô, trem e ônibus em horários de pico |
| **Problema, em uma frase** | Passageiros do transporte público sofrem diariamente com superlotação imprevisível, calor extremo nos vagões/pontos e falta de avisos em tempo real sobre elevadores e escadas rolantes quebrados |
| **Como funciona** | Sensores ESP32 nas estações enviam temperatura, umidade e contagem de pessoas para esta API. A API aplica regras de negócio, gera alertas (superlotação, calor, equipamento parado) e expõe tudo para o aplicativo mobile e para os painéis |
| **O que cada parte faz** | **Backend C# (este repositório):** API REST, regras de alerta, banco e indicadores. _[grupo: completar com Mobile, Physical Computing, IA/ML, Cybersecurity, SOA, Operating Systems e Testing]_ |
| **Link do repositório** | _[preencher]_ |
| **Indicador de impacto** | Redução do tempo de espera em estações superlotadas (min) e redução de viagens perdidas por PCD/idosos por elevadores inoperantes |
| **ODS conectados** | **ODS 9** (modernização da infraestrutura de transporte com IoT e dados), **ODS 10** (mobilidade digna para PCD, idosos e trabalhadores periféricos), **ODS 11** (transporte coletivo mais seguro, previsível e eficiente) |

**Integrantes:** _[preencher com os nomes]_

---

## O que o backend entrega

- **Estações** (metrô, trem, ônibus) com detalhe de acessibilidade, ocupação e temperatura atuais.
- **Equipamentos** (elevadores e escadas rolantes). Quando um equipamento fica indisponível, um **alerta de acessibilidade é aberto automaticamente**; quando volta a operar, é encerrado.
- **Leituras de sensor** (ESP32): cada leitura passa pelas regras de **superlotação** e **calor extremo**, gera alertas sem duplicar e encerra o alerta quando a situação normaliza.
- **Alertas** ordenados por gravidade.
- **Relatos colaborativos** dos passageiros, sem dados pessoais.
- **Indicadores de impacto**: % de equipamentos operantes, estações sem elevador funcionando, estações mais lotadas nas últimas 24 h.

> O tempo de espera real não é medido pelo protótipo. Usamos como **proxy** a ocupação das plataformas e a disponibilidade dos elevadores.

## Como rodar (Windows / PowerShell)

Pré-requisito: [.NET SDK 8 ou superior](https://dotnet.microsoft.com/download).

```powershell
git clone <link-do-repositorio>
cd Deslocai
dotnet restore
dotnet run --project src/Deslocai.Api
```

A API sobe em **http://localhost:5080** e abre o **Swagger** (`/swagger`). O banco SQLite (`deslocai.db`) é criado na primeira execução, já com **dados simulados** de estações de São Paulo (coordenadas aproximadas e capacidades fictícias).

### Testes

```powershell
dotnet test
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
| POST | `/api/leituras` | **Recebe leitura do ESP32** e devolve os alertas gerados |
| GET | `/api/estacoes/{id}/leituras?quantidade=10` | Últimas leituras |
| GET | `/api/alertas?estacaoId=&tipo=&incluirResolvidos=` | Alertas, do mais grave ao menos grave |
| PATCH | `/api/alertas/{id}/resolver` | Resolve um alerta |
| GET / POST | `/api/relatos` | Relatos dos passageiros |
| GET | `/api/indicadores/impacto` | Indicadores de impacto |

Erros seguem o padrão `ProblemDetails`: **400** (validação/regra de negócio), **404** (não encontrado), **409** (duplicidade).

### Exemplo: leitura do ESP32

```powershell
$corpo = @{ estacaoId = 1; temperaturaC = 37; umidadePercentual = 90; pessoasNaPlataforma = 3300 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/leituras -ContentType "application/json" -Body $corpo
```

No Wokwi, o ESP32 faz o mesmo `POST` com `HTTPClient` enviando esse JSON.

## Arquitetura

```
src/Deslocai.Api
├── Domain           Entidades, enums, exceções, IRepositorio e regras de alerta (IRegraAlerta)
├── Application      DTOs, interfaces e serviços (regras de aplicação)
├── Infrastructure   DbContext (EF Core + SQLite), repositório genérico, carga inicial
├── Controllers      Endpoints REST
└── Middleware       Tratamento global de exceções
tests/Deslocai.Tests Testes unitários (xUnit)
```

## Onde cada conceito da disciplina aparece

| Critério | Onde ver no código |
|---|---|
| **POO** (3,0) | Herança: `EntidadeBase`. Encapsulamento: setters privados e validação nos construtores (`Estacao`, `LeituraSensor`...). Polimorfismo e interfaces: `IRegraAlerta` com `RegraSuperlotacao` e `RegraCalorExtremo`. Exceções de domínio em `Domain/Excecoes.cs` |
| **Injeção de Dependência** | `Program.cs` registra repositório genérico, regras e serviços; tudo é recebido por construtor |
| **Web API** (2,5) | `Controllers/`, DTOs com validação, verbos HTTP e status corretos, `ProblemDetails`, Swagger com comentários XML |
| **EF Core + LINQ** (1,5) | `DeslocaiDbContext` (relações, índices únicos, cascata, conversão de enums). LINQ em `Application/*Service.cs`: filtros, `GroupBy`, `Distinct`, `Include`, projeções, agregações |
| **Async/Await** (1,0) | Toda a cadeia (controller → serviço → repositório) é assíncrona, com `CancellationToken` |
| **Qualidade do código** (2,0) | Camadas separadas, DTOs (a entidade nunca vaza), nomes claros, testes unitários |
| **Testes unitários** | `tests/Deslocai.Tests` (domínio, regras e serviços com EF InMemory) |

## Dados e privacidade

Todos os dados são **simulados**. O sistema não coleta dados pessoais: relatos guardam só o perfil genérico (idoso, PCD...) e um texto curto.

## Evidências da aplicação

Prints em [`docs/evidencias`](docs/evidencias) _[adicionar após rodar]_.
