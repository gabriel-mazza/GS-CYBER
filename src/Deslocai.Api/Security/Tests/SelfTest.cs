using System.Diagnostics;
using Deslocai.Api.Application;
using Deslocai.Security.Integration;
using Deslocai.Security.Pii;
using Microsoft.AspNetCore.Http;
using Deslocai.Security.Remediation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Deslocai.Security.Risk;
using Deslocai.Security.Taint;

namespace Deslocai.Security.Tests;


public static class SelfTest
{
    private static int _pass, _fail;

    private static void Check(string name, bool ok)
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "  OK  " : " FALHA")}  {name}");
    }

    private static List<PiiType> Types(string text) => new PiiDetector().Scan(text).Matches.Select(m => m.Type).ToList();

    public static int Run()
    {
        var d = new PiiDetector();
        Console.WriteLine("== Validadores ==");
        Check("CPF válido (529.982.247-25)", Validators.IsValidCpf("529.982.247-25"));
        Check("CPF inválido (dígito errado)", !Validators.IsValidCpf("529.982.247-26"));
        Check("CPF sequência repetida (111.111.111-11)", !Validators.IsValidCpf("111.111.111-11"));
        Check("CNPJ válido (11.222.333/0001-81)", Validators.IsValidCnpj("11.222.333/0001-81"));
        Check("CNPJ inválido", !Validators.IsValidCnpj("11.222.333/0001-82"));
        Check("Luhn válido (4111 1111 1111 1111)", Validators.IsValidLuhn("4111 1111 1111 1111"));
        Check("Luhn inválido", !Validators.IsValidLuhn("4111 1111 1111 1112"));

        Console.WriteLine("== Detecção ==");
        Check("CPF com pontuação", Types("meu cpf é 529.982.247-25").Contains(PiiType.Cpf));
        Check("CPF só dígitos", Types("cpf 52998224725").Contains(PiiType.Cpf));
        Check("CPF inválido NÃO detectado como CPF", !Types("doc 529.982.247-26").Contains(PiiType.Cpf));
        Check("Cartão válido", Types("cartão 4111 1111 1111 1111").Contains(PiiType.Cartao));
        Check("Cartão inválido não detectado", !Types("cartão 4111 1111 1111 1112").Contains(PiiType.Cartao));
        Check("E-mail", Types("fale com gabriel.silva@fiap.com.br hoje").Contains(PiiType.Email));
        Check("Telefone celular", Types("ligue (11) 98765-4321").Contains(PiiType.Telefone));
        Check("Telefone +55", Types("whats +55 11 98765-4321").Contains(PiiType.Telefone));
        Check("CEP", Types("CEP 06020-010").Contains(PiiType.Cep));
        Check("Placa Mercosul", Types("placa BRA2E19").Contains(PiiType.Placa));
        Check("Geolocalização", Types("estou em -23.5329, -46.7917").Contains(PiiType.Geolocalizacao));
        Check("RG com contexto", Types("RG: 12.345.678-9").Contains(PiiType.Rg));
        Check("Nascimento com contexto", Types("nascimento: 15/03/1950").Contains(PiiType.Nascimento));
        Check("Data sem contexto NÃO é PII", !Types("evento em 15/03/2026").Contains(PiiType.Nascimento));
        Check("Dado sensível PCD (LGPD art. 5º, II)", d.Scan("sou cadeirante").Matches.Any(m => m.Level == LgpdLevel.Sensivel));
        Check("Bilhete Único", Types("bilhete único 12345678901").Contains(PiiType.Bilhete));
        Check("CPF NÃO vira telefone (sobreposição)", !Types("cpf 529.982.247-25").Contains(PiiType.Telefone));
        Check("Texto limpo sem PII", !d.Scan("Elevador da estação Osasco está parado há 2 horas").HasPii);
        Check("Entrada vazia/nula", !d.Scan(null).HasPii && !d.Scan("").HasPii);

        Console.WriteLine("== Mascaramento ==");
        var r = d.Scan("CPF 529.982.247-25, email gabriel@fiap.com.br, tel (11) 98765-4321");
        Check("Máscara remove CPF", !r.Masked.Contains("529.982.247") && r.Masked.Contains("***.***.***-25"));
        Check("Máscara remove e-mail", !r.Masked.Contains("gabriel@") && r.Masked.Contains("g***@fiap.com.br"));
        Check("Máscara remove telefone", !r.Masked.Contains("98765") && r.Masked.Contains("4321"));

        Console.WriteLine("== Robustez ==");
        var sw = Stopwatch.StartNew();
        d.Scan(new string('1', 50_000) + new string('-', 20_000) + new string('a', 50_000));
        Check($"Entrada patológica 120KB em {sw.ElapsedMilliseconds} ms (< 3 s, sem ReDoS)", sw.ElapsedMilliseconds < 3000);
        var many = string.Join(" ", Enumerable.Repeat("cpf 529.982.247-25", 500));
        Check("500 CPFs no mesmo texto", d.Scan(many).Matches.Count(m => m.Type == PiiType.Cpf) == 500);

        Console.WriteLine("== Risco Cidadão ==");
        var leak = new Finding { Status = "Open", Sink = SinkKind.HttpOutbound, PiiTypes = { PiiType.Cpf }, VulnerableGroup = true };
        var safe = new Finding { Status = "Mitigated", Sanitized = true, Sink = SinkKind.Log, PiiTypes = { PiiType.Cpf } };
        var log = new Finding { Status = "Open", Sink = SinkKind.Log, PiiTypes = { PiiType.Email } };
        Check("Fluxo mitigado tem risco 0", RiskScoreService.FlowRaw(safe) == 0);
        Check("Vazamento a terceiro > vazamento em log", RiskScoreService.FlowRaw(leak) > RiskScoreService.FlowRaw(log));
        Check("Score em [0,100] e monotônico", RiskScoreService.ToScore(0) == 0 && RiskScoreService.ToScore(10) < RiskScoreService.ToScore(100) && RiskScoreService.ToScore(1e9) <= 100);

        Console.WriteLine("== Sanitizer do POST /api/relatos ==");
        DescricaoSanitizer Mk(bool mascarar, FindingStore st) => new(d, st, new HttpContextAccessor(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seguranca:MascararPiiEmRelatos"] = mascarar.ToString() }).Build());
        var st1 = new FindingStore();
        var saida = Mk(true, st1).Sanitizar("Elevador parado, sou cadeirante. Meu CPF 529.982.247-25 e tel (11) 98765-4321");
        Check("Mascara CPF e telefone na descrição", !saida.Contains("529.982") && !saida.Contains("98765") && saida.Contains("***.***.***-25"));
        Check("Preserva o conteúdo útil (cadeirante, elevador)", saida.Contains("cadeirante") && saida.Contains("Elevador parado"));
        var f1 = st1.Snapshot().Single();
        Check("Auditoria: fluxo Mitigado, PCD marcado como grupo vulnerável", f1.Status == "Mitigated" && f1.Sanitized && f1.VulnerableGroup && f1.Sink == SinkKind.Database);
        Check("Auditoria: evidência nunca contém PII real", !f1.EvidenceMasked.Contains("529.982"));
        var st2 = new FindingStore();
        var bruto = "Meu CPF 529.982.247-25";
        Check("Proteção desligada: texto intacto (cenário 'antes')", Mk(false, st2).Sanitizar(bruto) == bruto);
        var f2 = st2.Snapshot().Single();
        Check("Proteção desligada: fluxo ABERTO e evidência mascarada", f2.Status == "Open" && !f2.Sanitized && !f2.EvidenceMasked.Contains("529.982"));
        var st3 = new FindingStore();
        Check("Texto sem PII passa intacto e sem finding", Mk(true, st3).Sanitizar("Escada rolante parada na plataforma 2") == "Escada rolante parada na plataforma 2" && st3.Snapshot().Count == 0);
        var longo = string.Join(" ", Enumerable.Repeat("CEP 06020-010", 40));
        Check("Nunca excede 280 caracteres", Mk(true, new FindingStore()).Sanitizar(longo).Length <= 280);

        Console.WriteLine("== Senha, laudo/CID e Taint em modo Bloqueio ==");
        Check("Laudo: código CID-10 detectado e mascarado", Types("paciente com CID F32.1 em tratamento").Contains(PiiType.Laudo) && !d.Scan("CID F32.1").Masked.Contains("F32"));
        Check("Termo 'laudo médico' marca dado sensível", d.Scan("anexei o laudo médico").Matches.Any(m => m.Level == LgpdLevel.Sensivel));
        var tc = new TaintContext(d);
        tc.AddSource("body.senha", "MinhaSenha#2026");
        tc.AddSource("body.cpf", "529.982.247-25");
        Check("Senha tagueada pelo NOME da chave (sem regex de valor)", tc.Entries.Any(e => e.PiiTypes.Contains(PiiType.Senha)));
        Check("Redact apaga senha e CPF", !tc.Redact("senha=MinhaSenha#2026 cpf=529.982.247-25").Contains("MinhaSenha") && !tc.Redact("cpf=529.982.247-25").Contains("529.982"));
        SecurityMode Md(bool b) => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seguranca:ModoTaint"] = b ? "Bloqueio" : "Auditoria" }).Build());
        SinkGuard Gd(TaintContext c, FindingStore st, bool b) => new(c, st, new HttpContextAccessor(), d, Md(b), NullLogger<SinkGuard>.Instance);

        var stA = new FindingStore(); var gA = Gd(tc, stA, false);
        bool lancouA = false;
        try { gA.Sql("INSERT INTO R(Cpf) VALUES ('529.982.247-25')"); } catch (TaintBlockedException) { lancouA = true; }
        var fa = stA.Snapshot().Single();
        Check("AUDITORIA: registra vazamento (Open) e NÃO bloqueia", !lancouA && fa.Status == "Open" && !fa.Sanitized);

        var stB = new FindingStore(); var gB = Gd(tc, stB, true);
        bool lancouB = false;
        try { gB.Sql("INSERT INTO R(Cpf) VALUES ('529.982.247-25')"); } catch (TaintBlockedException) { lancouB = true; }
        var fb = stB.Snapshot().Single();
        Check("BLOQUEIO: SQL com PII é barrado (TaintBlockedException)", lancouB && fb.Status == "Blocked" && fb.Sanitized);
        Check("BLOQUEIO: evidência não contém PII", !fb.EvidenceMasked.Contains("529.982"));
        gB.Log("novo relato senha=MinhaSenha#2026 cpf 529.982.247-25");
        Check("BLOQUEIO: log vazando senha é registrado como bloqueado", stB.Snapshot().Any(f => f.Sink == SinkKind.Log && f.Status == "Blocked" && !f.EvidenceMasked.Contains("MinhaSenha")));
        var resp = gB.Response("{\"cpf\":\"529.982.247-25\",\"ok\":true}");
        Check("BLOQUEIO: resposta HTTP sai sem o CPF", !resp.Contains("529.982") && resp.Contains("ok"));
        var stC = new FindingStore();
        var respA = Gd(tc, stC, false).Response("{\"cpf\":\"529.982.247-25\"}");
        Check("AUDITORIA: resposta HTTP com PII é registrada e entregue (cenário 'antes')", respA.Contains("529.982") && stC.Snapshot().Any(f => f.Sink == SinkKind.HttpResponse && f.Status == "Open"));

        Console.WriteLine("== Tradução para o cidadão (modo offline) ==");
        var blocked = new Finding { Status = "Blocked", Sink = SinkKind.Log, PiiTypes = { PiiType.Laudo } };
        var msgB = CitizenTranslator.Offline(blocked);
        Check("Mensagem de bloqueio de saúde é humana e sem jargão", msgB.Contains("saúde") && msgB.Contains("bloque") && !msgB.Contains("PII") && !msgB.Contains("Sink"));
        Check("Mensagem tem 2 frases", msgB.Split('.', StringSplitOptions.RemoveEmptyEntries).Length == 2);
        Check("Descrição técnica enviada à IA não tem PII", !CitizenTranslator.Describe(blocked).Contains("529"));

        Console.WriteLine($"\n{_pass} OK, {_fail} falhas");
        return _fail == 0 ? 0 : 1;
    }
}
