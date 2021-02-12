using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public sealed class WebSocketApi : Api
    {
        private readonly ClientWebSocket socket;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly System.Collections.Generic.Dictionary<uint, TaskCompletionSource<System.Text.Json.JsonElement>> responses;
        private readonly Task receiveTask;
        private uint currentId = 0U;

        private async Task ReceiveLoop()
        {
            var response = new System.Buffers.ArrayBufferWriter<byte>();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var buffer = response.GetMemory();
                try
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationTokenSource.Token);
                    response.Advance(result.Count);
                    if (result.EndOfMessage)
                    {
                        var json = System.Text.Json.JsonDocument.Parse(response.WrittenMemory);

                        var type = json.RootElement.GetProperty("type").GetString();
                        if (type == "response")
                        {
                            var id = json.RootElement.GetProperty("id").GetUInt32();
                            var status = json.RootElement.GetProperty("status").GetString();
                            if (status == "success")
                            {
                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetResult(json.RootElement.GetProperty("result").Clone());
                                    }
                                }
                            }
                            else if (status == "error")
                            {
                                var error = json.RootElement.GetProperty("error").GetString();
                                var request = json.RootElement.GetProperty("request").Clone();
                                RippleException exception;
                                if (json.RootElement.TryGetProperty("error_exception", out var element))
                                {
                                    exception = new RippleSubmitRequestException(error, element.GetString(), request);
                                }
                                else
                                {
                                    exception = new RippleRequestException(error, request);
                                }

                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetException(exception);
                                    }
                                }
                            }
                            else
                            {
                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetException(new NotSupportedException(string.Format("{0} not a supported status", status)));
                                    }
                                }
                            }
                        }

                        response.Clear();
                    }
                }
                catch (TaskCanceledException taskCanceledException)
                {
                    if (taskCanceledException.CancellationToken == cancellationTokenSource.Token)
                    {
                        // We canceled the receive, while loop will now terminate and task completes successfully
                    }
                    else
                    {
                        // Something else unexpected was cancelled, rethrow
                        throw;
                    }
                }
            }
            socket.Dispose();
        }

        protected override uint WriteHeader(System.Text.Json.Utf8JsonWriter writer, string command)
        {
            var thisId = ++currentId;
            writer.WriteNumber("id", thisId);
            writer.WriteString("command", command);
            return thisId;
        }

        protected override void WriteFooter(System.Text.Json.Utf8JsonWriter writer)
        {
        }

        protected override async Task<System.Text.Json.JsonElement> SendReceiveAsync(uint requestId, ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<System.Text.Json.JsonElement>();
            lock (responses)
            {
                responses.Add(requestId, tcs);
            }
            await socket.SendAsync(json, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            return await tcs.Task;
        }

        public WebSocketApi(ClientWebSocket clientWebSocket)
        {
            socket = clientWebSocket;
            cancellationTokenSource = new CancellationTokenSource();
            responses = new System.Collections.Generic.Dictionary<uint, TaskCompletionSource<System.Text.Json.JsonElement>>();
            receiveTask = ReceiveLoop();
        }

        public async override ValueTask DisposeAsync()
        {
            cancellationTokenSource.Cancel();
            await receiveTask;
            await base.DisposeAsync();
        }
    }
}
