# Expérience API

## Objectif

Ajouter une couche "expérience" qui combine plusieurs appels vers des API de service en un seul endpoint.  
Le front-end appelle uniquement ce point d’entrée au lieu de faire plusieurs requêtes séparées.

---

## Endpoint : `/get-user-information`

Retourne les informations liées à l'utilisateur courant, incluant :

- les informations de l'utilisateur,
- ses permissions globales,
- ses comptes favoris.

---

## Exemple de réponse

```json
{
    "id": 25,
    "firstName": "Sofiane",
    "lastName": "Yousfi",
    "email": "syousfi@rydge.fr",
    "officePhone": "",
    "officeMobile": "2323244",
    "isCustomer": false,
    "oldId": "960ef70e-5181-4c6b-888d-b0bedccc65b8",
    "persona": {
        "id": 5,
        "name": "Collaborateur ESC",
        "description": null
    },
    "permissions": ["COADMI001", "COADMI002", "COADMI003"],
    "favoriteEntities": [
        {
            "accountId": 83,
            "accountNumber": "2024257956",
            "legalName": "Anika McBride",
            "iconName": "penny-icon"
        }
    ]
}
```
    
 ## Codes d'erreur
401 Unauthorized : Le token est invalide ou manquant.

400 Bad Request : Aucun contact associé au token n’a été trouvé.