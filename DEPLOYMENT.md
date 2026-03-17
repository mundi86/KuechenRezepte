# Deployment

## Zielbild

- Lokal: nur Test-/Entwicklungsinstanz
- Remote-Host `root@192.168.1.183`: produktive Instanz
- Produktiver Stack-Pfad: `/docker/KuechenRezepte`
- Start über `docker compose`
- Exponierter Port: `6655`

## Persistente Host-Daten

Die produktiven Daten liegen direkt auf dem Host:

- `/docker/KuechenRezepte/data`
- `/docker/KuechenRezepte/uploads`
- `/docker/KuechenRezepte/keys`

Das ist absichtlich ein Bind-Mount-Setup ohne Docker-Volumes.

## Laufzeit-Konfiguration

Die produktive Compose-Konfiguration steht in [`docker-compose.yml`](/c:/Users/mundi/Nextcloud/!Personal/!Projekte/#Programme/ASP.NET/KuechenRezepte/docker-compose.yml).

Wesentliche Punkte:

- `ConnectionStrings__DefaultConnection=Data Source=/data/KuechenRezepte.db`
- `/docker/KuechenRezepte/data` wird nach `/data` gemountet
- `/docker/KuechenRezepte/uploads` wird nach `/app/wwwroot/uploads` gemountet
- `/docker/KuechenRezepte/keys` wird nach `/home/appuser/.aspnet/DataProtection-Keys` gemountet

## Container-User

Das Image läuft als non-root User `appuser` mit UID `10001`.

Deshalb müssen die Host-Verzeichnisse dem Container-User gehören:

```bash
chown -R 10001:10001 /docker/KuechenRezepte/data \
                     /docker/KuechenRezepte/uploads \
                     /docker/KuechenRezepte/keys
```

## Erstinstallation

```bash
git clone <repo-url> /docker/KuechenRezepte
cd /docker/KuechenRezepte

mkdir -p /docker/KuechenRezepte/data \
         /docker/KuechenRezepte/uploads \
         /docker/KuechenRezepte/keys

chown -R 10001:10001 /docker/KuechenRezepte/data \
                     /docker/KuechenRezepte/uploads \
                     /docker/KuechenRezepte/keys

docker compose up -d --build
```

## Update

```bash
cd /docker/KuechenRezepte
git pull
docker compose up -d --build
```

## Verifikation

```bash
docker compose ps
curl -I http://127.0.0.1:6655/
```

Erwartung:

- Container `Up`
- HTTP-Status `200 OK`

## Hinweise

- Lokale Änderungen sind nicht automatisch produktiv, solange sie nicht committed und auf den Remote-Host gepullt wurden.
- Die App führt EF-Core-Migrationen beim Start automatisch aus.
- Die Seed-Daten werden beim ersten Start automatisch eingespielt.
