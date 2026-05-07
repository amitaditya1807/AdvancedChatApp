For API Gateway localhost to prod
comment/uncomment in program.cs
"Clusters": {
            "authCluster": {
                "Destinations": {
                    "d1": {
                        "Address": "https://advancedchatapp.onrender.com/"
                    }
                }
            },
            "chatCluster": {
                "Destinations": {
                    "d1": {
                        "Address": "https://chatservice-hanj.onrender.com/"
                    }
                }
            }
        }

https://localhost:7001/
https://localhost:5000/

