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
        private readonly string _outpunt;
        public IAServices(IOptions<WorkerConfig> config, IHttpClientFactory clientHttp)
        {

            _config = config.Value;
            _clientHttp = clientHttp.CreateClient("IAClient");
            _endpoint = _config.Endpoint;
            _apiKey = _config.ApiKey;
            _micrsoservicos = _config.Microservicos;
            _outpunt = _config.Output;
        }

        public async Task Run(string[] args)
        {


            var promptArquivoPorArquivoCodigo = File.ReadAllText("prompt-arquivo-por-arquivo-codigo.txt");

            await AnalisarArquivoPorArquivoCodigo(_micrsoservicos, promptArquivoPorArquivoCodigo);

            var promptArquivoPorArquivoTeste = File.ReadAllText("prompt-arquivo-por-arquivo-teste.txt");

            await AnalisarArquivoPorArquivoTestes(_micrsoservicos, promptArquivoPorArquivoTeste);

            var promptSumarizarCodigo = File.ReadAllText("prompt-sumarizar-codigo.txt");

            await SumarizarRecomendacaoPorMicroservicosCodigo(_micrsoservicos, promptSumarizarCodigo);

            var promptSumarizarTeste = File.ReadAllText("prompt-sumarizar-teste.txt");

            await SumarizarRecomendacaoPorMicroservicosTestes(_micrsoservicos, promptSumarizarTeste);

        }

        private async Task SumarizarRecomendacaoPorMicroservicosCodigo(List<string> microservicos, string prompt)
        {
            foreach (var microservico in microservicos)
            {
                var microservicoName = Path.GetFileName(microservico);
                var files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, microservicoName, "code"));
                await SumarizarRecomendacoes(microservicoName, prompt, files, "code");

            }
        }

        private async Task SumarizarRecomendacaoPorMicroservicosTestes(List<string> microservicos, string prompt)
        {
            foreach (var microservico in microservicos)
            {
                var microservicoName = Path.GetFileName(microservico);
                var files = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, microservicoName, "test"));
                await SumarizarRecomendacoes(microservicoName, prompt, files, "test");

            }
        }

        private async Task SumarizarRecomendacoes(string microservicoName, string prompt, string[] files, string diretorio)
        {
            var content = string.Empty;
            var parte = 0;
            var tamanho = 0D;
            foreach (var file in files)
            {

                content += File.ReadAllText(file);
                tamanho = CalcularTamanhoTextoEmKB(content);
                if (tamanho >= 500)
                {
                    content = string.Empty;
                    parte++;
                    await SumarizarRecomendacoesPoIA(microservicoName, content, parte.ToString(), prompt, diretorio);
                }
            }

            if (!string.IsNullOrEmpty(content))
            {
                await SumarizarRecomendacoesPoIA(microservicoName, content, (parte + 1).ToString(), prompt, diretorio);
            }
        }

        private async Task SumarizarRecomendacoesPoIA(string microservicoName, string content, string parte, string prompt, string diretorio)
        {
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
                                text = prompt
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


                    var arquivoName = parte;
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

                            var destino = Path.Combine(_outpunt,microservicoName, "resumo", diretorio);
                            if (!Directory.Exists(destino))
                                Directory.CreateDirectory(destino);

                            await File.WriteAllTextAsync($"{destino}/{parte}.json", jsonContext, Encoding.UTF8);
                        }
                    }
                }
            }
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

        private async Task AnalisarArquivoPorArquivoCodigo(List<string> micrsoservicos, string userPrompt)
        {
            foreach (var microservico in micrsoservicos)
            {
                Console.WriteLine($"Analisando o código microserviço: {microservico}");

                var pom = File.ReadAllText(Path.Combine(microservico, "pom.xml"));

                var arquivosJava = BuscarArquivosJava(microservico)
                    .Where(pathcomplete => !pathcomplete.Contains("test")).ToList(); ;


                foreach (string arquivo in arquivosJava)
                {

                    Console.WriteLine($"Analisando o arquivo: {arquivo}");
                    Console.WriteLine($"Prompt: {userPrompt}");

                    Console.WriteLine($"********************* Inicio da analise de código *********************");
                    Console.ForegroundColor = ConsoleColor.Green;

                    var java = File.ReadAllText(arquivo);

                    var systemPrompt = new object[] {
                    new {
                        type = "text",
                        text = "Você é um desenvolvedor de software com mais de 20 anos de experiência, especialista em Java e resiliência de código"
                    },
                    //new {
                    //    type = "text",
                    //    text = "Use o arquivo pom.xml da aplicação como apoio para as analises de dependencias:" + pom 
                    //},
                    new {
                        type = "text",
                        text = "Codigo para analise:" + java
                    },
                };

                    var messageContent = await BuscarRecomedacoesPorIA(userPrompt, systemPrompt);

                    var contentResult = new
                    {
                        microservico,
                        arquivo,
                        messageContent,

                    };

                    await SalvarRecomendacaoParcial(microservico, arquivo, "code", contentResult);

                    Console.ResetColor();
                    Console.WriteLine($"********************* Fim da analise *********************");

                }

            }
        }


        private async Task AnalisarArquivoPorArquivoTestes(List<string> micrsoservicos, string userPrompt)
        {
            foreach (var microservico in micrsoservicos)
            {
                Console.WriteLine($"Analisando os testes microserviço: {microservico}");

                var pom = File.ReadAllText(Path.Combine(microservico, "pom.xml"));

                var arquivosJava = BuscarArquivosJava(microservico)
                    .Where(pathcomplete => pathcomplete.Contains("test")).ToList(); ;


                foreach (string arquivo in arquivosJava)
                {

                    Console.WriteLine($"Analisando o arquivo: {arquivo}");
                    Console.WriteLine($"Prompt: {userPrompt}");

                    Console.WriteLine($"********************* Inicio da analise de testes *********************");
                    Console.ForegroundColor = ConsoleColor.Green;

                    var java = File.ReadAllText(arquivo);

                    var systemPrompt = new object[] {
                    new {
                        type = "text",
                        text = "Você é um profissional de testes sênior especializado em análise de qualidade de testes unitários."
                    },
                    //new {
                    //    type = "text",
                    //    text = "Use o arquivo pom.xml da aplicação como apoio para as analises de dependencias:" + pom 
                    //},
                    new {
                        type = "text",
                        text = "Codigo para analise:" + java
                    },
                };

                    var messageContent = await BuscarRecomedacoesPorIA(userPrompt, systemPrompt);

                    var contentResult = new
                    {
                        microservico,
                        arquivo,
                        messageContent,

                    };

                    await SalvarRecomendacaoParcial(microservico, arquivo, "test", contentResult);

                    Console.ResetColor();
                    Console.WriteLine($"********************* Fim da analise *********************");

                }

            }
        }

        private async Task SalvarRecomendacaoParcial(string microservico, string arquivo, string folder, dynamic result)
        {
            var microservicoName = Path.GetFileName(microservico);
            var arquivoName = Path.GetFileName(arquivo);
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

                    var destino = Path.Combine(_outpunt, microservicoName, "parcial", folder);
                    if (!Directory.Exists(destino))
                        Directory.CreateDirectory(destino);

                    await File.WriteAllTextAsync($"{destino}/{microservicoName}-{arquivoName}.json", jsonContext, Encoding.UTF8);
                }
            }
        }

        static List<string> BuscarArquivosJava(string diretorio)
        {
            List<string> arquivosJava = new List<string>();
            PercorrerDiretorio(diretorio, arquivosJava);
            return arquivosJava;
        }

        static void PercorrerDiretorio(string diretorio, List<string> arquivosJava)
        {
            try
            {
                foreach (string arquivo in Directory.GetFiles(diretorio, "*.java"))
                {
                    arquivosJava.Add(arquivo);
                }

                foreach (string subdir in Directory.GetDirectories(diretorio))
                {
                    PercorrerDiretorio(subdir, arquivosJava);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao acessar o diretório: {ex.Message}");
            }
        }

        private async Task<string> BuscarRecomedacoesPorIA(string userprompt, object[] systemPrompt)
        {


            //using (var httpClient = new HttpClient())
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



                    return messageContent;

                }

                throw new Exception($"Error: {response.StatusCode}, {response.ReasonPhrase}");
            }
        }


    }
}
