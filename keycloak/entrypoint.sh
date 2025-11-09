#!/bin/bash

/opt/keycloak/bin/kc.sh start-dev & 
echo "Waiting for Keycloak service to become available..."

# Loop until Keycloak is ready (EXISTING LOGIC)
while ! /opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8080 --realm master --user admin --password admin 2>/dev/null; do
    echo "Keycloak not ready yet, sleeping..."
    sleep 5
done

# 1. Apply the Master Realm fix (EXISTING LOGIC)
echo "Keycloak ready. Forcing master realm sslRequired=NONE via CLI..."
/opt/keycloak/bin/kcadm.sh update realms/master -s sslRequired=NONE

# 2. 💡 NEW: Force the creation of the new realm using the JSON file
echo "Importing new realm (myrealm) from JSON file..."
/opt/keycloak/bin/kcadm.sh create realms -f /tmp/myrealm-import.json --server http://localhost:8080 --realm master --user admin --password admin

echo "Configuration applied. Keycloak is running."
wait