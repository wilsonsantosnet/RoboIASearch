using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace RoboIASearch
{
    public class IAServices
    {
        private readonly WorkerConfig _config;
        private readonly HttpClient _clientHttp;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly List<string> _micrsoservicos;
        private readonly string _output;
        private int _attempt;
        private int _maxRetries;
        public IAServices(IOptions<WorkerConfig> config, IHttpClientFactory clientHttp)
        {

            _config = config.Value;
            _clientHttp = clientHttp.CreateClient("IAClient");
            _endpoint = _config.Endpoint;
            _apiKey = _config.ApiKey;
            _micrsoservicos = _config.Microservicos;
            _output = _config.Output;
            this._attempt = 0;
            this._maxRetries = 10;
        }

        public async Task Run(string[] args)
        {

            var prompts = new List<dynamic>
            {
                new {
                    userPrompt = File.ReadAllText("prompt-arquivo-por-arquivo-codigo.pro"),
                    tipo = "code",
                    metodo = Metodo.AnalisarArquivoPorArquivoCodigo,
                    enable = true,
                    enablePom = true,
                    enableYaml = true,
                },
                new {
                    userPrompt = File.ReadAllText("prompt-arquivo-por-arquivo-codigo-v2.pro"),
                    tipo = "codev2",
                    metodo = Metodo.AnalisarArquivoPorArquivoCodigo,
                    enable = true,
                    enablePom = false,
                    enableYaml = false,
                },
                new {
                    userPrompt = File.ReadAllText("prompt-arquivo-por-arquivo-codigo-v3.pro"),
                    tipo = "codev3",
                    metodo = Metodo.AnalisarArquivoPorArquivoCodigo,
                    enable = true,
                    enablePom = false,
                    enableYaml = false,
                },

                new {
                    userPrompt = File.ReadAllText("prompt-arquivo-por-arquivo-teste.pro"),
                    tipo = "test",
                    metodo = Metodo.AnalisarArquivoPorArquivoTestes,
                    enable = true
                },
                new {
                    userPrompt = File.ReadAllText("prompt-sumarizar-codigo.pro"),
                    tipo = "code",
                    metodo = Metodo.SumarizarRecomendacaoPorMicroservicos,
                    enable = true,
                    tiposParaSumarizar = new string[] {"code"}
                },
                new {
                    userPrompt = File.ReadAllText("prompt-sumarizar-codigo.pro"),
                    tipo = "codev2",
                    metodo = Metodo.SumarizarRecomendacaoPorMicroservicos,
                    enable = true,
                    tiposParaSumarizar = new string[] {"codev2"}
                },
                new {
                    userPrompt = File.ReadAllText("prompt-sumarizar-codigo.pro"),
                    tipo = "codev3",
                    metodo = Metodo.SumarizarRecomendacaoPorMicroservicos,
                    enable = true,
                    tiposParaSumarizar = new string[] {"codev3"}
                },
                new {
                    userPrompt = File.ReadAllText("prompt-sumarizar-teste.pro"),
                    tipo = "test",
                    metodo = Metodo.SumarizarRecomendacaoPorMicroservicos,
                    enable = true,
                    tiposParaSumarizar = new string[] {"test" }
                },
                new {
                    userPrompt = File.ReadAllText("prompt-arquivo-por-arquivo-pom.pro"),
                    tipo = "dep",
                    metodo = Metodo.AnalisarArquivoPorArquivo,
                    enable = true,
                    arquivoParaAnalise = "pom.xml"
                }
            };

            foreach (dynamic prompt in prompts)
            {

                switch (prompt.metodo)
                {
                    case Metodo.AnalisarArquivoPorArquivoCodigo:
                        if (prompt.enable)
                            await AnalisarArquivoPorArquivoCodigo(_micrsoservicos, prompt);
                        break;
                    case Metodo.AnalisarArquivoPorArquivoTestes:
                        if (prompt.enable)
                            await AnalisarArquivoPorArquivoTestes(_micrsoservicos, prompt);
                        break;
                    case Metodo.AnalisarArquivoPorArquivo:
                        if (prompt.enable)
                            await AnalisarArquivoPorArquivo(_micrsoservicos, prompt);
                        break;
                    case Metodo.SumarizarRecomendacaoPorMicroservicos:
                        if (prompt.enable)
                            await SumarizarRecomendacaoPorMicroservicos(_micrsoservicos, prompt);
                        break;
                    case Metodo.SumarizarTodasRecomendacoes:
                        if (prompt.enable)
                            await SumarizarTodasRecomendacoes(_micrsoservicos, prompt);
                        break;
                    default:
                        // Código para caso não haja correspondência
                        break;
                }
            }




            GeraIndice();
        }


        private async Task SumarizarRecomendacaoPorMicroservicos(List<string> microservicos, dynamic prompt)
        {
            foreach (var microservico in microservicos)
            {
                Console.WriteLine($"Sumarizando analises: {microservico}");

                var microservicoName = Path.GetFileName(microservico);
                string tipo = prompt.tipo;
                string destino = Path.Combine(_output, microservicoName, "resumo", tipo);
                

                if (!Directory.Exists(destino))
                    Directory.CreateDirectory(destino);

                if (Directory.GetFiles(destino, "*.json").Any() && Directory.GetFiles(destino, "*.html").Any())
                    continue;

                var files = new List<string>();
                foreach (string item in prompt.tiposParaSumarizar)
                {
                    var filesitem = Directory.GetFiles(Path.Combine(_output, microservicoName, "parcial", item), "*.json").ToList();
                    files.AddRange(filesitem);
                }

                var result = await SumarizarRecomendacoes(microservicoName, prompt, files, tipo);

                if (!Directory.GetFiles(destino, "*.json").Any())
                    await SalvaResumoRecomendacoesJson(result, destino, microservicoName);

                if (!Directory.GetFiles(destino, "*.html").Any())
                    await SalvaResumoRecomendacoesHtml(result, destino, microservicoName, prompt);

            }
        }

        private async Task SumarizarTodasRecomendacoes(List<string> microservicos, dynamic prompt)
        {
            Console.WriteLine($"Sumarizando todas as Analises");

            var files = BuscarArquivos(_output, "*-pom.xml.json");
            var result = await SumarizarRecomendacoes("geral", prompt.userPrompt, files, prompt.tipo);

            var destino = Path.Combine(_output, "resumo-geral", prompt.tipo);
            await SalvaResumoRecomendacoesJson(result, destino, "geral");

        }


        private async Task<dynamic> SumarizarRecomendacoes(string microservicoName, dynamic prompt, List<string> files, string tipo)
        {
            var content = string.Empty;
            var messageContent = string.Empty;
            var parte = 0;
            var tamanho = 0D;
            foreach (var file in files)
            {

                content += File.ReadAllText(file);
                tamanho = CalcularTamanhoTextoEmKB(content);
                if (tamanho >= 500)
                {

                    parte++;
                    messageContent = await SumarizarRecomendacoesPoIA(microservicoName, content, parte.ToString(), prompt, tipo, tamanho);
                    content = string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(content))
            {
                tamanho = CalcularTamanhoTextoEmKB(content);
                messageContent = await SumarizarRecomendacoesPoIA(microservicoName, content, (parte + 1).ToString(), prompt, tipo, tamanho);
            }

            return new
            {
                messageContent,
                parte,
                tamanho
            };
        }



        private async Task<string> SumarizarRecomendacoesPoIA(string microservicoName, string content, string parte, dynamic prompt, string diretorio, double tamanho)
        {

            try
            {
                return await SumarizarRecomendacoesPoIATentativa(microservicoName, content, parte, prompt, diretorio, tamanho);
            }
            catch (Exception ex)
            {

                if (_attempt < _maxRetries)
                {
                    _attempt++;
                    var esperar = Backoff();
                    Thread.Sleep(esperar);
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Tentativa {_attempt} erro: {ex.Message} esperar:{esperar}");
                    Console.ResetColor();
                    return await SumarizarRecomendacoesPoIA(microservicoName, content, parte, prompt, diretorio, tamanho);
                }

            }

            _attempt = 0;
            throw new Exception($"Error: Retentativas não deu certo");

        }

        private async Task<string> SumarizarRecomendacoesPoIATentativa(string microservicoName, string content, string parte, dynamic prompt, string diretorio, double tamanho)
        {
            Console.WriteLine($"Sumarizando parte : {parte}");
            Console.WriteLine($"Tamanho : {tamanho}");
            Console.WriteLine($"Prompt: {prompt.userPrompt}");

            //using (var httpClient = new HttpClient())
            {
                _clientHttp.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
                var payload = new
                {
                    messages = new object[]
                    {
                    new {
                        role = "system",
                        content = new object[] {
                            new {
                                type = "text",
                                text = prompt.userPrompt
                            },
                        }
                    },
                    new {
                        role = "user",
                        content = new object[] {
                            new {
                                type = "text",
                                text = content
                            }
                        }
                        }
                },

                    temperature = 0.7,
                    top_p = 0.95,
                    max_tokens = 2000,
                    stream = false
                };

                var response = await _clientHttp.PostAsync(_endpoint, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var responseData = JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());

                    // Carregar o JSON
                    var data = JsonDocument.Parse(responseData.ToString());


                    // Extrair informações relevantes
                    var messageContent = data.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    var completionTokens = data.RootElement.GetProperty("usage").GetProperty("completion_tokens").GetInt32();
                    var promptTokens = data.RootElement.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
                    var totalTokens = data.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32();

                    // Exibir as informações
                    Console.WriteLine("Conteúdo da Mensagem:");
                    Console.WriteLine(messageContent);
                    Console.WriteLine("****");
                    Console.WriteLine("Usage:");
                    Console.WriteLine("Completion Tokens: " + completionTokens);
                    Console.WriteLine("Prompt Tokens: " + promptTokens);
                    Console.WriteLine("Total Tokens: " + totalTokens);

                    return messageContent;

                }
            }

            throw new Exception("Sem response da API");
        }


        private static string GetFilePathResumoRecomendacoes(string destino, int parte, double tamanho, string microservicoName, string ext)
        {
            if (!Directory.Exists(destino))
                Directory.CreateDirectory(destino);

            var pathResumoRecomendacoes = $"{destino}/{microservicoName}-{parte}-{Math.Ceiling(tamanho)}.{ext}";
            return pathResumoRecomendacoes;
        }

        public static double CalcularTamanhoTextoEmKB(string text)
        {
            // Converte o texto em um array de bytes usando a codificação UTF-8
            byte[] byteArray = Encoding.UTF8.GetBytes(text);

            // Calcula o tamanho em bytes
            double sizeInBytes = byteArray.Length;

            // Converte o tamanho de bytes para kilobytes (1 KB = 1024 bytes)
            double sizeInKB = sizeInBytes / 1024;

            return sizeInKB;
        }

        private async Task AnalisarArquivoPorArquivoCodigo(List<string> micrsoservicos, dynamic prompt)
        {
            foreach (var microservico in micrsoservicos)
            {
                Console.WriteLine($"Analisando o código microserviço: {microservico}");

                var pom = string.Empty;
                var yaml = string.Empty;

                if (prompt.enablePom)
                    pom = File.ReadAllText(Path.Combine(microservico, "pom.xml"));

                if (prompt.enableYaml)
                {
                    var ymlPath = Path.Combine(microservico, "src//main//resources//application.yml");
                    var yamlPath = Path.Combine(microservico, "src//main//resources//application.yaml");

                    if (File.Exists(ymlPath))
                        yamlPath = ymlPath;


                    yaml = File.ReadAllText(yamlPath);
                }

                var arquivosJava = BuscarArquivos(microservico, "*.java")
                    .Where(pathcomplete => !pathcomplete.Contains("test")).ToList();


                foreach (string arquivo in arquivosJava)
                {

                    var filePathRecomedacaoParcialJson = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "json");
                    var filePathRecomedacaoParcialHtml = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "html");
                    if (File.Exists(filePathRecomedacaoParcialJson) && File.Exists(filePathRecomedacaoParcialHtml))
                        continue;

                    Console.WriteLine($"Analisando o arquivo: {arquivo}");
                    Console.WriteLine($"Prompt: {prompt.userPrompt}");

                    Console.WriteLine($"********************* Inicio da analise de código *********************");
                    Console.ForegroundColor = ConsoleColor.Green;

                    var java = File.ReadAllText(arquivo);

                    var systemPrompt = new object[] {
                        new {
                            type = "text",
                            text = "Você é um desenvolvedor de software com mais de 20 anos de experiência, especialista em Java e resiliência de código"
                        },
                        new {
                            type = "text",
                            text = "Arquivo pom.xml:" + pom
                        },
                        new {
                            type = "text",
                            text = "Arquivo application.yaml:" + yaml
                        },
                        new {
                            type = "text",
                            text = "Codigo para analise:" + java
                        },
                         new {
                            type = "text",
                            text = "Por favor responder sempre em português"
                        },
                    };

                    var messageContent = await BuscarRecomedacoesPorIA(prompt.userPrompt, systemPrompt);

                    var contentResult = new
                    {
                        microservico,
                        arquivo,
                        messageContent,

                    };

                    if (!File.Exists(filePathRecomedacaoParcialJson))
                        await SalvarRecomendacaoParcialJson(microservico, arquivo, prompt, contentResult);

                    if (!File.Exists(filePathRecomedacaoParcialHtml))
                        await SalvarRecomendacaoParcialHtml(microservico, arquivo, prompt, contentResult);

                    Console.ResetColor();
                    Console.WriteLine($"********************* Fim da analise *********************");

                }

            }
        }

        private async Task AnalisarArquivoPorArquivo(List<string> micrsoservicos, dynamic prompt)
        {
            foreach (var microservico in micrsoservicos)
            {
                Console.WriteLine($"Analisando o código microserviço: {microservico}");

                var arquivos = BuscarArquivos(microservico, prompt.arquivoParaAnalise);



                foreach (string arquivo in arquivos)
                {

                    Console.WriteLine($"Analisando o arquivo: {arquivo}");
                    Console.WriteLine($"Prompt: {prompt.userPrompt}");

                    Console.WriteLine($"********************* Inicio da analise de código *********************");
                    Console.ForegroundColor = ConsoleColor.Green;

                    var content = File.ReadAllText(arquivo);

                    var systemPrompt = new object[] {
                    new {
                        type = "text",
                        text = "Você é um desenvolvedor de software com mais de 20 anos de experiência, especialista em Java e resiliência de código"
                    },
                    new {
                        type = "text",
                        text = "Codigo para analise:" + content
                    },
                };

                    var messageContent = await BuscarRecomedacoesPorIA(prompt.userPrompt, systemPrompt);

                    var contentResult = new
                    {
                        microservico,
                        arquivo,
                        messageContent,

                    };

                    await SalvarRecomendacaoParcialJson(microservico, arquivo, prompt, contentResult);

                    Console.ResetColor();
                    Console.WriteLine($"********************* Fim da analise *********************");

                }

            }
        }

        private async Task AnalisarArquivoPorArquivoTestes(List<string> micrsoservicos, dynamic prompt)
        {
            foreach (var microservico in micrsoservicos)
            {
                Console.WriteLine($"Analisando os testes microserviço: {microservico}");


                var arquivosJava = BuscarArquivos(microservico, "*.java")
                    .Where(pathcomplete => pathcomplete.Contains("test")).ToList(); ;


                foreach (string arquivo in arquivosJava)
                {
                    var filePathRecomedacaoParcialJson = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "json");
                    var filePathRecomedacaoParcialHtml = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "html");
                    if (File.Exists(filePathRecomedacaoParcialJson) && File.Exists(filePathRecomedacaoParcialHtml))
                        continue;

                    Console.WriteLine($"Analisando o arquivo: {arquivo}");
                    Console.WriteLine($"Prompt: {prompt.userPrompt}");

                    Console.WriteLine($"********************* Inicio da analise de testes *********************");
                    Console.ForegroundColor = ConsoleColor.Green;

                    var java = File.ReadAllText(arquivo);

                    var systemPrompt = new object[] {
                    new {
                        type = "text",
                        text = "Você é um profissional de testes sênior especializado em análise de qualidade de testes unitários."
                    },
                    new {
                        type = "text",
                        text = "Codigo para analise:" + java
                    },
                };

                    var messageContent = await BuscarRecomedacoesPorIA(prompt.userPrompt, systemPrompt);

                    var contentResult = new
                    {
                        microservico,
                        arquivo,
                        messageContent,

                    };

                    if (!File.Exists(filePathRecomedacaoParcialJson))
                        await SalvarRecomendacaoParcialJson(microservico, arquivo, prompt, contentResult);

                    if (!File.Exists(filePathRecomedacaoParcialHtml))
                        await SalvarRecomendacaoParcialHtml(microservico, arquivo, prompt, contentResult);

                    Console.ResetColor();
                    Console.WriteLine($"********************* Fim da analise *********************");

                }

            }
        }


        private string GetFilePathRecomedacaoParcial(string tipo, string microservico, string arquivo, string ext)
        {
            var microservicoName = Path.GetFileName(microservico);
            var arquivoName = Path.GetFileName(arquivo);

            var destino = Path.Combine(_output, microservicoName, "parcial", tipo);
            if (!Directory.Exists(destino))
                Directory.CreateDirectory(destino);

            var filePathRecomedacaoParcial = $"{destino}/{microservicoName}-{arquivoName}.{ext}";
            return filePathRecomedacaoParcial;
        }

        static List<string> BuscarArquivos(string diretorio, string filtro)
        {
            List<string> arquivos = new List<string>();
            PercorrerDiretorio(diretorio, arquivos, filtro);
            return arquivos;
        }

        static void PercorrerDiretorio(string diretorio, List<string> arquivos, string filtro)
        {
            try
            {
                foreach (string arquivo in Directory.GetFiles(diretorio, filtro))
                {
                    arquivos.Add(arquivo);
                }

                foreach (string subdir in Directory.GetDirectories(diretorio))
                {
                    PercorrerDiretorio(subdir, arquivos, filtro);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao acessar o diretório: {ex.Message}");
            }
        }

        private async Task<string> BuscarRecomedacoesPorIA(string userprompt, object[] systemPrompt)
        {

            try
            {
                return await BuscarRecomendacoesPorIATentativa(userprompt, systemPrompt);
            }
            catch (Exception ex)
            {

                if (_attempt < _maxRetries)
                {
                    _attempt++;
                    var esperar = Backoff();
                    Thread.Sleep(esperar);
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Tentativa {_attempt} erro: {ex.Message} esperar:{esperar}");
                    Console.ResetColor();
                    return await BuscarRecomedacoesPorIA(userprompt, systemPrompt);
                }

            }

            _attempt = 0;
            throw new Exception($"Error: Retentativas não deu certo");

        }

        private int Backoff()
        {
            return 60000 * (int)Math.Pow(2, _attempt - 1);
        }

        private async Task<string> BuscarRecomendacoesPorIATentativa(string userprompt, object[] systemPrompt)
        {
            _clientHttp.DefaultRequestHeaders.Add("api-key", _apiKey);
            var payload = new
            {
                messages = new object[]
                {
                    new {
                        role = "system",
                        content = systemPrompt
                    },
                    new {
                        role = "user",
                        content = new object[] {
                            new {
                                type = "text",
                                text = userprompt
                            }
                        }
                    }
                },
                //response_format = new { type = "json_object" },
                temperature = 0.7,
                top_p = 0.95,
                max_tokens = 2000,
                stream = false
            };

            var response = await _clientHttp.PostAsync(_endpoint, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var responseData = JsonSerializer.Deserialize<object>(await response.Content.ReadAsStringAsync());

                // Carregar o JSON
                var data = JsonDocument.Parse(responseData.ToString());


                // Extrair informações relevantes
                var messageContent = data.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                var completionTokens = data.RootElement.GetProperty("usage").GetProperty("completion_tokens").GetInt32();
                var promptTokens = data.RootElement.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
                var totalTokens = data.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32();

                // Exibir as informações
                Console.WriteLine("Conteúdo da Mensagem:");
                Console.WriteLine(messageContent);
                Console.WriteLine("****");
                Console.WriteLine("Usage:");
                Console.WriteLine("Completion Tokens: " + completionTokens);
                Console.WriteLine("Prompt Tokens: " + promptTokens);
                Console.WriteLine("Total Tokens: " + totalTokens);


                _attempt = 0;
                return messageContent;

            }

            throw new Exception($"Error: {response.StatusCode}, {response.ReasonPhrase}");
        }

        #region salvar

        private async Task SalvaResumoRecomendacoesHtml(dynamic result, string destino, string microservicoName, dynamic prompt)
        {

            Console.WriteLine($"Gerando arquivo html");

            var template = File.ReadAllText("TemplateHtml.html");

            var contentHtml = template
                .Replace("<#microservico#>", Path.GetFileName(microservicoName))
                .Replace("<#arquivo#>", "N/D")
                .Replace("<#userPrompt#>", prompt.userPrompt)
                .Replace("<#conteudo#>", result.messageContent);



            var pathResumoRecomendacoes = GetFilePathResumoRecomendacoes(destino, result.parte, result.tamanho, microservicoName, "html");

            await File.WriteAllTextAsync(pathResumoRecomendacoes, contentHtml, Encoding.UTF8);


        }

        private async Task SalvaResumoRecomendacoesJson(dynamic messageContent, string destino, string microservicoName)
        {

            using (var stream = new MemoryStream())
            {
                var options = new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
                };
                using (var writer = new Utf8JsonWriter(stream, options))
                {
                    JsonSerializer.Serialize(writer, messageContent);
                }
                stream.Position = 0;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var jsonContext = await reader.ReadToEndAsync();

                    var pathResumoRecomendacoes = GetFilePathResumoRecomendacoes(destino, messageContent.parte, messageContent.tamanho, microservicoName, "json");

                    await File.WriteAllTextAsync(pathResumoRecomendacoes, jsonContext, Encoding.UTF8);
                }
            }
        }

        private async Task SalvarRecomendacaoParcialHtml(string microservico, string arquivo, dynamic prompt, dynamic result)
        {
            Console.WriteLine($"Gerando arquivo html");

            var template = File.ReadAllText("TemplateHtml.html");

            var contentHtml = template
                .Replace("<#microservico#>", Path.GetFileName(microservico))
                .Replace("<#arquivo#>", Path.GetFileName(arquivo))
                .Replace("<#userPrompt#>", prompt.userPrompt)
                .Replace("<#conteudo#>", result.messageContent);

            var filePathRecomedacaoParcial = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "html");

            await File.WriteAllTextAsync(filePathRecomedacaoParcial, contentHtml, Encoding.UTF8);
        }

        private async Task SalvarRecomendacaoParcialJson(string microservico, string arquivo, dynamic prompt, dynamic result)
        {
            Console.WriteLine($"Gerando arquivo json");

            using (var stream = new MemoryStream())
            {
                var options = new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
                };
                using (var writer = new Utf8JsonWriter(stream, options))
                {
                    JsonSerializer.Serialize(writer, result);
                }
                stream.Position = 0;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var jsonContext = await reader.ReadToEndAsync();

                    var filePathRecomedacaoParcial = GetFilePathRecomedacaoParcial(prompt.tipo, microservico, arquivo, "json");

                    await File.WriteAllTextAsync(filePathRecomedacaoParcial, jsonContext, Encoding.UTF8);
                }
            }
        }
        #endregion region

        #region indice


        private void GeraIndice()
        {
            List<string> arquivos = BuscarArquivos(_output, "*.*");

            // Separar os arquivos por pasta e subpasta
            var arquivosPorPasta = arquivos
                .GroupBy(arquivo => Path.GetDirectoryName(arquivo))
                .ToDictionary(grupo => grupo.Key, grupo => grupo.ToList());


            var html = GerarHtml(arquivosPorPasta);

            File.WriteAllText(Path.Combine(_output, "indice.html"), html, Encoding.UTF8);
        }
        string GerarHtml(Dictionary<string, List<string>> arquivosPorPasta)
        {
            var links = string.Empty;

            // Ordenar as pastas para garantir a hierarquia correta
            var pastasOrdenadas = arquivosPorPasta.Keys.OrderBy(pasta => pasta);

            foreach (var pasta in pastasOrdenadas)
            {
                links += GerarHtmlParaPasta(pasta, arquivosPorPasta);
            }


            Console.WriteLine($"Gerando arquivo html indice");

            var template = File.ReadAllText("TemplateHtmlindice.html");

            var contentHtml = template
                .Replace("<#links#>", links);


            return contentHtml;

        }

        string GerarHtmlParaPasta(string pasta, Dictionary<string, List<string>> arquivosPorPasta)
        {
            var html = $"<h2>{pasta}</h2>";
            html += "<ul>";

            foreach (var arquivo in arquivosPorPasta[pasta].OrderBy(_ => Path.GetExtension(_)))
            {
                html += $"<li><a href={Path.GetRelativePath(_output, arquivo)}>{Path.GetFileName(arquivo)}</a></li>";
            }
            html += "</ul>";
            // Encontrar subpastas
            var subpastas = arquivosPorPasta.Keys
                .Where(p => p.StartsWith(pasta) && p != pasta)
                .OrderBy(p => p);

            foreach (var subpasta in subpastas)
            {
                html += GerarHtmlParaPasta(subpasta, arquivosPorPasta);
            }

            return html;
        }
        #endregion
    }

}
