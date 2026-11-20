namespace Deslocai.Api.Domain;

public enum TipoTransporte { Metro, Trem, Onibus }

public enum TipoEquipamento { Elevador, EscadaRolante }

public enum StatusEquipamento { Operante, Inoperante, EmManutencao }

public enum TipoAlerta { Superlotacao, CalorExtremo, EquipamentoInoperante }


public enum Severidade { Baixa = 1, Media = 2, Alta = 3, Critica = 4 }

public enum TipoRelato { Superlotacao, Calor, EquipamentoQuebrado, Outro }

public enum PerfilCidadao { Geral, Idoso, Pcd, Estudante, Trabalhador }
