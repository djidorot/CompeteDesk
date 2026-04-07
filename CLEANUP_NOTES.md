# Cleanup notes

The updated solution removes the placeholder `CompeteDesk.Data` and `CompeteDesk.Domain` projects from `CompeteDesk.sln` and adds `.gitignore` rules to stop junk files from coming back.

To fully finish repository cleanup, delete these leftover placeholder files and folders from the repo when applying the patch:

- `CompeteDesk.Web/CompeteDesk.Data/`
- `CompeteDesk.Web/CompeteDesk.Domain/`
- any committed `.DS_Store` files
