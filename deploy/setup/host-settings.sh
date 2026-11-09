#!/bin/bash
# Secret-bearing JSON goes directly to dotnet user-secrets stdin. Never log it.
set -euo pipefail
kind=${1:?Executable name required}
root=${DELIVERYPILOT_HOST_SECRETS_DIR:?Host secret-directory path required}
frontend=${DELIVERYPILOT_FRONTEND_ORIGIN:-http://localhost:3000}
observability=${DELIVERYPILOT_OBSERVABILITY_ENABLED:-false}
case "$kind" in
  gateway)
    jq -n --arg port "${DELIVERYPILOT_GATEWAY_PORT:-19090}" \
      --arg identity "http://127.0.0.1:${DELIVERYPILOT_IDENTITY_API_PORT:-19091}/" \
      --arg notification "http://127.0.0.1:${DELIVERYPILOT_NOTIFICATION_API_PORT:-19092}/" \
      --arg frontend "$frontend" --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
      '{"urls":("http://localhost:"+$port),"Cors:AllowedOrigins:0":$frontend,
        "ReverseProxy:Clusters:identity:Destinations:identity-api:Address":$identity,
        "ReverseProxy:Clusters:notification:Destinations:notification-api:Address":$notification,
        "Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}'
    ;;
  identity-api|identity-worker|identity-migrationrunner)
    role=${kind#identity-}
    [ "$role" != migrationrunner ] || role=migrator
    jq -n \
      --arg connection "Host=127.0.0.1;Port=${DELIVERYPILOT_IDENTITY_POSTGRES_PORT:?};Database=DeliveryPilotIdentity;Username=identity_${role};Include Error Detail=false" \
      --arg password_file "$root/identity/${role}-password.secret" \
      --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
      '{"ConnectionStrings:ApplicationDb":$connection,"Database:PasswordFile":$password_file,"Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}' >/tmp/host-base.json
    if [ "$kind" = identity-api ]; then
      jq \
        --rawfile jwt /source/identity/jwt-key.secret \
        --rawfile integration /source/identity/integration-key.secret \
        --rawfile dispatch /source/identity/invitation-dispatch-key.secret \
        --arg public_key "$root/notification/keys/${DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID:-local-v1}.public.pem" \
        --arg key_id "${DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID:-local-v1}" \
        --arg issuer "${DELIVERYPILOT_JWT_ISSUER:-DeliveryPilot.Identity}" \
        --arg audience "${DELIVERYPILOT_JWT_AUDIENCE:-DeliveryPilot}" \
        --arg frontend "$frontend" \
        --arg urls "http://localhost:${DELIVERYPILOT_IDENTITY_API_PORT:?}" \
        '. + {"Jwt:SecretKey":($jwt|gsub("[\\r\\n]";"")),"Jwt:Issuer":$issuer,"Jwt:Audience":$audience,
              "IntegrationApi:ApiKeys:0":($integration|gsub("[\\r\\n]";"")),
              "InvitationDispatch:ApiKey":($dispatch|gsub("[\\r\\n]";"")),
              "InvitationDeliveryProducer:KeyId":$key_id,"InvitationDeliveryProducer:PublicKeyFile":$public_key,
              "Cors:AllowedOrigins:0":$frontend,"Cors:AllowedMethods:0":"GET","Cors:AllowedMethods:1":"POST",
              "Cors:AllowedHeaders:0":"Content-Type","Cors:AllowedHeaders:1":"Authorization",
              "Serilog:Using:0":"Serilog.Sinks.Console","Serilog:WriteTo:0:Name":"Console","urls":$urls}' /tmp/host-base.json
    elif [ "$kind" = identity-worker ]; then
      jq --arg port "${DELIVERYPILOT_RABBITMQ_AMQP_PORT:?}" --arg vhost "${DELIVERYPILOT_MESSAGING_VIRTUAL_HOST:-/deliverypilot-local}" \
        --arg password_file "$root/platform/identity-broker-password.secret" \
        --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
        '. + {"Messaging:Host":"127.0.0.1","Messaging:Port":$port,"Messaging:VirtualHost":$vhost,
              "Messaging:Username":"identity","Messaging:PasswordFile":$password_file,
              "Messaging:Development":"true","Messaging:UseTls":"false","Messaging:QuorumSize":"1",
              "Observability:Endpoint":$endpoint,"Observability:ServiceName":"DeliveryPilot.Identity.Worker"}' /tmp/host-base.json
    else
      cat /tmp/host-base.json
    fi
    ;;
  projects-api|projects-worker|projects-migrationrunner)
    role=${kind#projects-}
    [ "$role" != migrationrunner ] || role=migrator
    jq -n \
      --arg connection "Host=127.0.0.1;Port=${DELIVERYPILOT_PROJECTS_POSTGRES_PORT:-25433};Database=DeliveryPilotProjects;Username=projects_${role};Include Error Detail=false" \
      --arg password_file "$root/projects/${role}-password.secret" \
      --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
      '{"ConnectionStrings:ApplicationDb":$connection,"Database:PasswordFile":$password_file,"Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}' >/tmp/host-base.json
    if [ "$kind" = projects-api ]; then
      jq --rawfile jwt /source/identity/jwt-key.secret \
        --rawfile outbound /source/identity/integration-key.secret \
        --rawfile inbound /source/projects/integration-key.secret \
        --arg issuer "${DELIVERYPILOT_JWT_ISSUER:-DeliveryPilot.Identity}" \
        --arg audience "${DELIVERYPILOT_JWT_AUDIENCE:-DeliveryPilot}" \
        --arg identity "http://127.0.0.1:${DELIVERYPILOT_IDENTITY_API_PORT:-19091}/" \
        --arg frontend "$frontend" --arg urls "http://localhost:${DELIVERYPILOT_PROJECTS_API_PORT:-19093}" \
        '. + {"Jwt:SecretKey":($jwt|gsub("[\\r\\n]";"")),"Jwt:Issuer":$issuer,"Jwt:Audience":$audience,
          "Integrations:Identity:BaseUrl":$identity,"Integrations:Identity:ApiKeyHeaderName":"X-Integration-Key",
          "Integrations:Identity:ApiKey":($outbound|gsub("[\\r\\n]";"")),"IntegrationApi:ApiKeys:0":($inbound|gsub("[\\r\\n]";"")),
          "Cors:AllowedOrigins:0":$frontend,"Cors:AllowedMethods:0":"POST","Cors:AllowedMethods:1":"GET","Cors:AllowedMethods:2":"PUT",
          "Cors:AllowedHeaders:0":"Content-Type","Cors:AllowedHeaders:1":"Authorization","Cors:AllowedHeaders:2":"Idempotency-Key",
          "Serilog:Using:0":"Serilog.Sinks.Console","Serilog:WriteTo:0:Name":"Console","urls":$urls}' /tmp/host-base.json
    elif [ "$kind" = projects-worker ]; then
      jq --arg port "${DELIVERYPILOT_RABBITMQ_AMQP_PORT:?}" --arg vhost "${DELIVERYPILOT_MESSAGING_VIRTUAL_HOST:-/deliverypilot-local}" \
        --arg password_file "$root/platform/projects-broker-password.secret" \
        '. + {"Messaging:Host":"127.0.0.1","Messaging:Port":$port,"Messaging:VirtualHost":$vhost,
          "Messaging:Username":"projects","Messaging:PasswordFile":$password_file,"Messaging:Development":"true",
          "Messaging:UseTls":"false","Messaging:QuorumSize":"1","Observability:ServiceName":"DeliveryPilot.Projects.Worker"}' /tmp/host-base.json
    else
      cat /tmp/host-base.json
    fi
    ;;
  notification-api|notification-worker|notification-migrationrunner)
    role=runtime
    [ "$kind" != notification-migrationrunner ] || role=migrator
    jq -n \
      --arg connection "Host=127.0.0.1;Port=${DELIVERYPILOT_NOTIFICATION_POSTGRES_PORT:?};Database=DeliveryPilotNotification;Username=notification_${role}" \
      --arg password_file "$root/notification/${role}-password.secret" \
      --arg observability "$observability" --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
      '{"ConnectionStrings:NotificationDb":$connection,"Database:PasswordFile":$password_file,"Observability:Enabled":$observability,"Observability:Endpoint":$endpoint}' >/tmp/host-base.json
    if [ "$kind" = notification-migrationrunner ]; then
      cat /tmp/host-base.json
    else
      jq \
        --arg mode "${DELIVERYPILOT_EMAIL_MODE:-Mailpit}" --arg sender "${DELIVERYPILOT_EMAIL_SENDER:-DeliveryPilot <auth@example.test>}" --arg verified "${DELIVERYPILOT_EMAIL_DOMAIN_VERIFIED:-false}" \
        --arg frontend "$frontend" --arg key_directory "$root/notification/keys" --arg key_id "${DELIVERYPILOT_EMAIL_ACTIVE_KEY_ID:-local-v1}" \
        --arg port "${DELIVERYPILOT_RABBITMQ_AMQP_PORT:?}" --arg vhost "${DELIVERYPILOT_MESSAGING_VIRTUAL_HOST:-/deliverypilot-local}" \
        --arg smtp_port "${DELIVERYPILOT_MAILPIT_SMTP_PORT:?}" --arg urls "http://localhost:${DELIVERYPILOT_NOTIFICATION_API_PORT:?}" \
        --arg daily_limit "${DELIVERYPILOT_EMAIL_DAILY_LIMIT:-100}" --arg monthly_limit "${DELIVERYPILOT_EMAIL_MONTHLY_LIMIT:-3000}" \
        --arg password_file "$root/platform/notification-broker-password.secret" \
        --arg dispatch_key_file "$root/identity/invitation-dispatch-key.secret" \
        --arg identity_origin "http://127.0.0.1:${DELIVERYPILOT_IDENTITY_API_PORT:-19091}/" \
        --arg endpoint "http://127.0.0.1:${DELIVERYPILOT_OTLP_PORT:-24317}" \
        '. + {"Notification:EmailDelivery:Mode":$mode,"Notification:EmailDelivery:Development":"true",
              "Notification:EmailDelivery:Sender":$sender,"Notification:EmailDelivery:FrontendOrigin":$frontend,
              "Notification:EmailDelivery:KeyDirectory":$key_directory,"Notification:EmailDelivery:ActiveKeyId":$key_id,
              "Notification:EmailDelivery:RecipientHashKeyFile":($key_directory+"/recipient-hmac.secret"),
              "Notification:EmailDelivery:ApiKeyFile":($key_directory+"/resend-api.secret"),
              "Notification:EmailDelivery:WebhookSecretFile":($key_directory+"/resend-webhook.secret"),
              "Notification:EmailDelivery:DomainVerified":$verified,
              "Notification:EmailDelivery:MailpitHost":"127.0.0.1","Notification:EmailDelivery:MailpitPort":$smtp_port,
              "Notification:EmailDelivery:DailyLimit":$daily_limit,"Notification:EmailDelivery:MonthlyLimit":$monthly_limit,
              "InvitationDispatch:IdentityOrigin":$identity_origin,"InvitationDispatch:ApiKeyFile":$dispatch_key_file,
              "Messaging:Host":"127.0.0.1","Messaging:Port":$port,"Messaging:VirtualHost":$vhost,
              "Messaging:Username":"notification","Messaging:PasswordFile":$password_file,
              "Messaging:Development":"true","Messaging:UseTls":"false","Messaging:QuorumSize":"1",
              "Observability:Endpoint":$endpoint,"urls":$urls}' /tmp/host-base.json
    fi
    ;;
  *) echo 'Unknown executable for IDE configuration.' >&2; exit 2 ;;
esac
