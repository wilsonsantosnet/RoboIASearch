Objetivo:

	Buscar dentro do código fornecido dependências externas, tais como:

		-Banco de dados
		-Cache Redis
		-Chamadas HTTP
		-Consumo de filas, como Azure Service Bus

Tarefas:

	-Identificação de Dependências:
		Verifique se o código contém as dependências mencionadas acima.

	-Análise de Resiliência:
		Caso encontre essas dependências, analise se existem boas práticas de resiliência implementadas.
		Gere uma lista com recomendações de melhorias.

	-Exemplos de Código:
		Crie exemplos de código para Retry, Retry exponencial e Circuit Breaker.
		Se houver Retry com fallbacks que não retornam exceções, sugira substituir por Circuit Breaker.

Condições:
	Se não encontrar as dependências citadas, não forneça sugestões.

Exclusões:

	Não exiba o código fornecido.
	Não forneça recomendações para recursos que não existem no código.