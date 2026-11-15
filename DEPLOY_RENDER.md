# Deploy the ASP.NET application to Render

This is an ASP.NET Core MVC application backed by SQL Server. Deploy the
application as a Render Docker web service and use a separately hosted,
persistent SQL Server-compatible database. The local Docker container and
database on a development PC are not accessible from Render.

## Prepare a production database

Create a production SQL Server database with a trusted provider, such as Azure
SQL Database. Configure its firewall to allow the Render service's outbound
addresses. Prefer a Render static outbound IP feature where available; do not
open the database to the entire internet.

Create the database in the provider first. Apply the schema from
`DatabaseSetup.sql` to that database using a SQL Server client. For providers
that do not allow `CREATE DATABASE` or `USE`, omit those initial statements and
connect the client directly to the new database. Run the setup script only
against a fresh database. It inserts a development administrator account; replace
that credential before exposing the service publicly. Do not migrate real voter
data unless authorized and protected by appropriate legal and security controls.

## Create the Render web service

1. Push the repository to a Git provider and connect it in Render.
2. Create a **Web Service**, select **Docker**, and use the repository root as
   the build context. The root `Dockerfile` builds the main `VotingSystem.csproj`.
3. The container listens on port `10000`; do not override its start command.
4. Add the following environment variables in Render's service settings:

   - `ConnectionStrings__DefaultConnection`: the production SQL Server
     connection string. Use the database provider's TLS settings and keep this
     value private.
   - `Seo__PublicBaseUrl`: the public HTTPS origin, for example
     `https://your-service.onrender.com` (no trailing slash).
   - `Seo__Author`: the developer's preferred public name.
   - Other required integration keys as individual secret environment variables.

5. Deploy and inspect both build and runtime logs. The app deliberately exits
   with a clear error if `ConnectionStrings__DefaultConnection` is missing.
6. Check `/`, `/robots.txt`, and `/sitemap.xml`; then test login, registration,
   database access, and OCR with non-sensitive test data.

If the Git repository contains the project inside nested folders, set Render's
Root Directory to the folder containing this `Dockerfile` and
`VotingSystem.csproj`.

## Google Search

The homepage has a descriptive title, summary, author, canonical URL, and social
preview metadata. The sitemap lists only the public homepage, and shared
application pages are marked `noindex`. Set `Seo__PublicBaseUrl` to the actual
HTTPS URL before deployment.

Add a custom domain if desired, verify it in Google Search Console, and submit
`https://your-domain/sitemap.xml`. Search indexing and ranking are controlled by
Google and are not guaranteed or immediate. Describe this as a project
demonstration unless authorized to represent it as an official government
service.
