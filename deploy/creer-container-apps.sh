#!/usr/bin/env bash
#
# Creation initiale de l'environnement Container Apps et des deux applications.
#
# A executer UNE FOIS. Ensuite, les workflows se contentent de
# `az containerapp update`, qui exige que l'application existe deja.
#
#   ./deploy/creer-container-apps.sh
#
# Les deux applications vivent dans le meme environnement : l'API n'a donc qu'une entree
# INTERNE, joignable par le frontend a http://archipelago-api sans passer par Internet.
# C'est ce qui evite le hairpinning qui bloquait la version Azure Container Instances.
#
# min-replicas 0 fait dormir les conteneurs quand personne ne s'en sert : la charge d'un
# tournoi amical tient dans le quota mensuel gratuit du plan Consumption. Le prix est un
# demarrage a froid de quelques secondes apres une periode d'inactivite.

set -euo pipefail

GROUPE="${GROUPE:-archipelago}"
REGION="${REGION:-canadaeast}"
ENVIRONNEMENT="${ENVIRONNEMENT:-archipelago-env}"
REGISTRE="${REGISTRE:?definir REGISTRE, par exemple noresetspeedrun.azurecr.io}"

# Identifiants admin du registre : az acr credential show -n <registre>
REGISTRE_UTILISATEUR="${REGISTRE_UTILISATEUR:?definir REGISTRE_UTILISATEUR}"
REGISTRE_MOTDEPASSE="${REGISTRE_MOTDEPASSE:?definir REGISTRE_MOTDEPASSE}"

echo "Extension containerapp..."
az extension add --name containerapp --upgrade --only-show-errors

echo "Fournisseurs de ressources..."
az provider register --namespace Microsoft.App --wait
az provider register --namespace Microsoft.OperationalInsights --wait

# Container Apps n'est pas disponible partout : la commande echoue si la region ne le
# supporte pas, auquel cas prendre canadacentral.
echo "Environnement $ENVIRONNEMENT dans $REGION..."
az containerapp env create \
    --name "$ENVIRONNEMENT" \
    --resource-group "$GROUPE" \
    --location "$REGION"

# L'API : entree interne uniquement, jamais exposee sur Internet.
echo "Application archipelago-api..."
az containerapp create \
    --name archipelago-api \
    --resource-group "$GROUPE" \
    --environment "$ENVIRONNEMENT" \
    --image "$REGISTRE/archipelago-api:latest" \
    --registry-server "$REGISTRE" \
    --registry-username "$REGISTRE_UTILISATEUR" \
    --registry-password "$REGISTRE_MOTDEPASSE" \
    --ingress internal \
    --target-port 8080 \
    --min-replicas 0 \
    --max-replicas 2 \
    --cpu 0.5 --memory 1.0Gi \
    --env-vars ASPNETCORE_ENVIRONMENT=Production

# Le frontend : seule application exposee publiquement, avec HTTPS fourni par Azure.
echo "Application archipelago-web..."
az containerapp create \
    --name archipelago-web \
    --resource-group "$GROUPE" \
    --environment "$ENVIRONNEMENT" \
    --image "$REGISTRE/archipelago-web:latest" \
    --registry-server "$REGISTRE" \
    --registry-username "$REGISTRE_UTILISATEUR" \
    --registry-password "$REGISTRE_MOTDEPASSE" \
    --ingress external \
    --target-port 80 \
    --min-replicas 0 \
    --max-replicas 2 \
    --cpu 0.25 --memory 0.5Gi \
    --env-vars API_URL=http://archipelago-api

echo
echo "Termine. Adresse publique du site :"
az containerapp show \
    --name archipelago-web \
    --resource-group "$GROUPE" \
    --query properties.configuration.ingress.fqdn -o tsv

echo
echo "Les workflows prennent le relais : ils mettent a jour l'image et les secrets."
