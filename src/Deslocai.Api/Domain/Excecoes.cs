namespace Deslocai.Api.Domain;


public class RegraDeNegocioException : Exception
{
    public RegraDeNegocioException(string mensagem) : base(mensagem) { }
}


public class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem) { }
}


public class RecursoNaoEncontradoException : Exception
{
    public RecursoNaoEncontradoException(string recurso, object id)
        : base($"{recurso} com id '{id}' não foi encontrado(a).") { }
}
