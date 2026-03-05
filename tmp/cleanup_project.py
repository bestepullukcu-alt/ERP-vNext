import os
import re

legacy_controllers = [
    "ActiveIngredientController.cs",
    "AgreementController.cs",
    "CalendarController.cs",
    "CategoryController.cs",
    "CompanyController.cs",
    "CountryController.cs",
    "DocumentationSystemController.cs",
    "EmailController.cs",
    "MasterDataController.cs",
    "PPMController.cs",
    "PharmaceuticalFormController.cs",
    "ProductRecordController.cs",
    "PvSystemController.cs",
    "RegistrationController.cs",
    "RegulatoryAffairController.cs",
    "SafetyReportController.cs",
    "SurveyController.cs",
    "UserManagementController.cs"
]

modern_controllers = [
    "HomeController.cs",
    "AccountController.cs",
    "AuthController.cs",
    "UserListController.cs",
    "LegalEntitiesController.cs"
]

controllers_path = "/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Controllers"
archive_path = os.path.join(controllers_path, "Archive")

# Move legacy controllers and update namespace
for controller in legacy_controllers:
    src = os.path.join(controllers_path, controller)
    dst = os.path.join(archive_path, controller)
    
    if os.path.exists(src):
        with open(src, 'r') as f:
            content = f.read()
        
        # Update namespace to Diten.Web.Controllers.Archive
        # It could be Diten.Web.WebUI.Controllers or Diten.Web.Controllers
        content = re.sub(r'namespace\s+Diten\.Web(\.WebUI)?\.Controllers', 'namespace Diten.Web.Controllers.Archive', content)
        
        with open(dst, 'w') as f:
            f.write(content)
        
        os.remove(src)
        print(f"Archived and updated namespace: {controller}")

# Update modern controllers namespace
for controller in modern_controllers:
    path = os.path.join(controllers_path, controller)
    if os.path.exists(path):
        with open(path, 'r') as f:
            content = f.read()
        
        # Standardize to Diten.Web.Controllers
        content = re.sub(r'namespace\s+Diten\.Web\.WebUI\.Controllers', 'namespace Diten.Web.Controllers', content)
        
        with open(path, 'w') as f:
            f.write(content)
        print(f"Standardized namespace: {controller}")

# Handle ErrorViewModel
error_vm_path = "/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Models/ErrorViewModel.cs"
if os.path.exists(error_vm_path):
    with open(error_vm_path, 'r') as f:
        content = f.read()
    content = re.sub(r'namespace\s+Diten\.Web\.WebUI\.Models', 'namespace Diten.Web.Models', content)
    with open(error_vm_path, 'w') as f:
        f.write(content)
    print("Standardized namespace: ErrorViewModel.cs")
