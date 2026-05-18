${moduleName}
==========================

Description
-----------
This is a very basic module which can be used as a starting point in creating a new module.

Building from Source
--------------------
You will need to have Java 1.6+ and Maven 2.x+ installed.  Use the command 'mvn package' to 
compile and package the module.  The .omod file will be in the omod/target folder.

Alternatively you can add the snippet provided in the [Creating Modules](https://wiki.openmrs.org/x/cAEr) page to your 
omod/pom.xml and use the mvn command:

    mvn package -P deploy-web -D deploy.path="../../openmrs-1.8.x/webapp/src/main/webapp"

It will allow you to deploy any changes to your web 
resources such as jsp or js files without re-installing the module. The deploy path says 
where OpenMRS is deployed.

Running Spotless
----------------
This project uses Spotless for code formatting. Spotless is embedded in the build process, so when you run `mvn clean package`, Spotless will automatically format your code according to the project's style guidelines.

If you want to run Spotless separately, you can use the following Maven commands:

To apply the formatting:

    mvn spotless:apply

This will automatically format your code according to the project's style guidelines. It's recommended to run this command before committing your changes.

To check if your code adheres to the style guidelines without making any changes, you can run:

    mvn spotless:check

If this command reports any violations, you can then run `mvn spotless:apply` to fix them.

Remember, in most cases, you don't need to run these commands separately as Spotless will run automatically during the build process with `mvn clean package`.

Installation
------------
1. Build the module to produce the .omod file.
2. Use the OpenMRS Administration > Manage Modules screen to upload and install the .omod file.

If uploads are not allowed from the web (changable via a runtime property), you can drop the omod
into the ~/.OpenMRS/modules folder.  (Where ~/.OpenMRS is assumed to be the Application 
Data Directory that the running openmrs is currently using.)  After putting the file in there 
simply restart OpenMRS/tomcat and the module will be loaded and started.

Webhook secrets
---------------
This module sends webhook requests and needs two secrets: `apiKey` and `tenantId`.

Option A (UI, easiest): OpenMRS Global Properties
-------------------------------------------------
You do not need a custom screen: OpenMRS already has an admin UI for configuration.

1. Go to **Administration**
2. Open **Advanced Settings** (or **Manage Global Properties** depending on your distro)
3. Search for `patientpingeling.` and create / edit these Global Properties:

- `patientpingeling.apiKey`
- `patientpingeling.tenantId`

Note: the module will auto-create these Global Properties (with empty values) on startup if they do not exist yet. If you don't see them immediately, restart OpenMRS or reload the module.

The module will read these values at runtime, so you can change them without rebuilding the `.omod`.

Security note: Global Properties are stored in the OpenMRS database and may be visible to admins. Treat them as secrets.

Option B (file): External JSON file
-----------------------------------
Because a `.omod` is a packaged artifact, you typically cannot “edit a file inside the module” after installing it.
Instead, provide the secrets as an external JSON file:

- **Recommended default location**: `${OPENMRS_APPLICATION_DATA_DIRECTORY}/patientpingeling/pp-secrets.json`
    - The application data directory is often `~/.OpenMRS` on Linux/macOS (but depends on your OpenMRS setup).
- **Override**: set environment variable `PP_SECRETS_FILE` to the full path of your JSON file (useful for Docker secrets/mounts).

JSON format:

        {
            "apiKey": "...",
            "tenantId": "..."
        }

Fallback (legacy): environment variables
--------------------------------------
If neither Global Properties nor a JSON file are configured, the module falls back to:

- `PP_API_KEY`
- `PP_TENANT_KEY`

Service account credentials
--------------------------
This module needs to authenticate to OpenMRS to enrich events. Do **not** hardcode credentials in the code.

Docker Compose: environment variables
------------------------------------
Set these environment variables for the OpenMRS container (for example via your `.env` file):

- `PP_SERVICE_USER`
- `PP_SERVICE_PASSWORD`
