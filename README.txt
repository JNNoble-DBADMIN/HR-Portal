LDAP/AD Gateway + Reverse Proxy (Windows Server 2022 containers)
===============================================================

What this is
------------
A small ASP.NET Core gateway container that:
  - Provides a portal UI at http://portal.rel-cicd-01
  - Prompts for AD credentials (LDAP bind to 10.240.131.12)
  - Sets a gateway session cookie
  - Reverse-proxies:
      shuttle.rel-cicd-01      -> http://app1:8082/
      workstation.rel-cicd-01  -> http://app2:8087/
  - Blocks app1/app2 unless logged-in.

IMPORTANT
---------
- This does NOT remove App1/App2 internal login screens (no changes to apps).
- This is the "main gate" only.

Configure
---------
1) Edit docker-compose.yml:
   - Replace YOUR_APP1_IMAGE and YOUR_APP2_IMAGE with your images.
   - Confirm app1 listens on 8082 inside the container, app2 on 8087.

2) If you can use LDAPS, change:
   - Ldap__Port=636
   - Ldap__UseSsl=true

Run
---
  docker compose up -d --build

Test
----
  http://portal.rel-cicd-01  -> login
  http://shuttle.rel-cicd-01 -> requires gateway login
  http://workstation.rel-cicd-01 -> requires gateway login
