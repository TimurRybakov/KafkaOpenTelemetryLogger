using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var seqPassword = builder.AddParameter("seq-admin-password", "test", secret: true).ExcludeFromManifest();
var seqApiKey = "devseqkey12345678";

var seq = builder.AddSeq("seq", seqPassword, port: 5341)
    .WithEnvironment("SEQ_API_KEY", seqApiKey)
    .WithLifetime(ContainerLifetime.Persistent);

var kafka = builder.AddKafka("kafka", port: 9092)
    .WithKafkaUI()
    .WithDataVolume("otlp-kafka", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent);

var openSearchPassword = "I_j63c9fa2392a9568ee41";
var openSearch = builder.AddContainer("opensearch", "opensearchproject/opensearch", "3.3.2")
    .WithEnvironment("cluster.name", "opensearch-dev")
    .WithEnvironment("node.name", "opensearch-node")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("bootstrap.memory_lock", "true")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", openSearchPassword)
    //.WithBindMount("opensearch-data", "/usr/share/opensearch/data")
    //.WithBindMount("./opensearch.yml", "/usr/share/opensearch/config/opensearch.yml")
    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
    .WithEndpoint(port: 9200, targetPort: 9200, name: "http")
    .WithEndpoint(port: 9600, targetPort: 9600, name: "perf")
    .WithLifetime(ContainerLifetime.Persistent);

var openSearchDashboard = builder.AddContainer("opensearch-dashboards", "opensearchproject/opensearch-dashboards", "3.3.0")
    .WithHttpEndpoint(port: 5601, targetPort: 5601)
    .WithEnvironment("OPENSEARCH_HOSTS", "[\"https://opensearch:9200\"]")
    .WithLifetime(ContainerLifetime.Persistent);

var collector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib", "0.138.0")
    .WithHttpEndpoint(targetPort: 4318, name: "otlp-http")
    .WithEndpoint(targetPort: 4317, name: "otlp-grpc")
    .WithBindMount("./otel-collector-config.yaml", "/etc/otelcol-config.yaml")
    .WithReference(seq)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitFor(openSearch)
    .WithEnvironment("KAFKA_BROKERS", "kafka:9093")
    .WithEnvironment("SEQ_ENDPOINT", "http://seq:5341/ingest/otlp")
    .WithEnvironment("SEQ_API_KEY", seqApiKey)
    .WithEnvironment("OPENSEARCH_USERNAME", "admin")
    .WithEnvironment("OPENSEARCH_PASSWORD", openSearchPassword)
    .WithArgs("--config", "/etc/otelcol-config.yaml");

var bService = builder.AddProject<KafkaOpenTelemetryLogger_BService>("b-service")
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

var aService = builder.AddProject<KafkaOpenTelemetryLogger_AService>("a-service")
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.GetEndpoint("otlp-http").Url;
        context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
    })
    .WithReference(kafka)
    .WithReference(seq)
    .WithReference(bService)
    .WaitFor(kafka)
    .WaitFor(collector)
    .WaitFor(openSearch)
    .WaitFor(seq)
    .WaitFor(bService);

builder.Build().Run();
