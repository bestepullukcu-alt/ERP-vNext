# Subscription Feature Management User Manual

Use Platform > Subscription Features to manage commercial feature definitions and their availability across plans. The page has two tabs: **Categories** and **Features**.

## Feature Catalog

The **Features** tab lists features in a table with status and category filters. Use **Create Feature** to open a full-page form, or the row actions to edit, view details, deactivate, or archive a feature. Creating or editing a feature opens a dedicated page (`/Platform/SubscriptionFeatures/Create` or `/Edit/{id}`); the read-only details open at `/Details/{id}`.

## Categories

The **Categories** tab lists feature categories in a table. Use **Create Category** (or a row's **Edit**) to open a side panel where you set the code, display name, description, sort order, and status. The category code cannot be changed after creation. Categories can be **archived** (kept for history, removed from active use) but not hard-deleted. Create categories before assigning features to them.

## Plan Availability

Use the plan availability area to review or update where a feature is available. This controls plan configuration; it does not directly grant access to a tenant without the subscription and entitlement flows.

## Archive And Deactivate

Deactivate a feature when it should temporarily stop being offered. Archive a feature when it should be retained for history but removed from active management.

