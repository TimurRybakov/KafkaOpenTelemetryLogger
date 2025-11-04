using System.Net.Sockets;
using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var seqPassword = builder.AddParameter("seq-admin-password", "test", secret: true);
var seqApiKey = "devseqkey12345678";

var seq = builder.AddSeq("seq", seqPassword, port: 5341)
    .WithEnvironment("SEQ_API_KEY", seqApiKey)
    .WithLifetime(ContainerLifetime.Persistent);

var kafka = builder.AddKafka("kafka", port: 9092)
    .WithKafkaUI()
    .WithDataVolume("otlp-kafka", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent);

var collector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "0.138.0")
    .WithHttpEndpoint(targetPort: 4318, name: "otlp-http")
    .WithEndpoint(targetPort: 4317, name: "otlp-grpc")
    .WithBindMount("./otel-collector-config.yaml", "/etc/otelcol-config.yaml")
    .WithReference(seq)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("KAFKA_BROKERS", "kafka:9093")
    .WithEnvironment("SEQ_ENDPOINT", "http://seq:5341/ingest/otlp")
    .WithEnvironment("SEQ_API_KEY", seqApiKey)
    .WithArgs("--config", "/etc/otelcol-config.yaml");

//var openSearchPassword = builder.AddParameter("seq-admin-password", "test", secret: true);
var openSearch = builder.AddContainer("opensearch", "opensearchproject/opensearch", "3.3.2")
    .WithEnvironment("cluster.name", "opensearch-dev")
    .WithEnvironment("node.name", "opensearch-node")
    .WithEnvironment("discovery.type", "single-node") // ← важно для одной ноды
    .WithEnvironment("bootstrap.memory_lock", "true")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", "test")
    .WithBindMount("opensearch-data", "/usr/share/opensearch/data")
    .WithEndpoint(port: 9200, targetPort: 9200, name: "http")
    .WithEndpoint(port: 9600, targetPort: 9600, name: "perf")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddContainer("opensearch-dashboards", "opensearchproject/opensearch-dashboards", "2.19.3")
    .WithEndpoint(port: 5601, targetPort: 5601)
    .WithEnvironment("OPENSEARCH_HOSTS", "[\"https://opensearch:9200\"]");

var aService = builder.AddProject<KafkaOpenTelemetryLogger_AService>("a-service")
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.GetEndpoint("otlp-http").Url;
        context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
    })
    .WithReference(kafka)
    .WithReference(seq)
    .WaitFor(kafka)
    .WaitFor(collector)
    .WaitFor(openSearch)
    .WaitFor(seq);

builder.Build().Run();
