using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class NotificationTemplateSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);

        foreach (var template in CreatePlatformDefaults())
        {
            var exists = await collection.Find(x =>
                    x.IsDeleted == false &&
                    x.TenantId == null &&
                    x.IsPlatformDefault &&
                    x.Status == NotificationTemplateStatus.Active &&
                    x.Channel == template.Channel &&
                    x.Locale == template.Locale &&
                    x.TemplateKey == template.TemplateKey)
                .AnyAsync(ct);

            if (exists)
            {
                continue;
            }

            await collection.InsertOneAsync(template, cancellationToken: ct);
        }
    }

    private static IReadOnlyList<NotificationTemplate> CreatePlatformDefaults()
    {
        return
        [
            TenantInvite("en"),
            TenantInvite("tr"),
            TenantSuspended("en"),
            TenantSuspended("tr"),
            TenantReactivated("en"),
            TenantReactivated("tr"),

            /*
             * MOD-0024 task notifications (WC-4), SEVEN languages each.
             *
             * Without these the dispatch path is dead on arrival: QueueEmailNotificationHandler answers 404 when
             * no template resolves, and NO dispatch record is even created — so "the notification did not
             * arrive" looks identical to "the notification was never attempted". A tenant surface ships all
             * seven languages; two would put an English email in front of five sets of readers.
             */
            TaskAssigned("en"),
            TaskAssigned("tr"),
            TaskAssigned("fr"),
            TaskAssigned("es"),
            TaskAssigned("zh"),
            TaskAssigned("ar"),
            TaskAssigned("ru"),
            TaskClaimed("en"),
            TaskClaimed("tr"),
            TaskClaimed("fr"),
            TaskClaimed("es"),
            TaskClaimed("zh"),
            TaskClaimed("ar"),
            TaskClaimed("ru"),
            TaskDueSoon("en"),
            TaskDueSoon("tr"),
            TaskDueSoon("fr"),
            TaskDueSoon("es"),
            TaskDueSoon("zh"),
            TaskDueSoon("ar"),
            TaskDueSoon("ru"),
            TaskCompleted("en"),
            TaskCompleted("tr"),
            TaskCompleted("fr"),
            TaskCompleted("es"),
            TaskCompleted("zh"),
            TaskCompleted("ar"),
            TaskCompleted("ru"),
            TaskCommented("en"),
            TaskCommented("tr"),
            TaskCommented("fr"),
            TaskCommented("es"),
            TaskCommented("zh"),
            TaskCommented("ar"),
            TaskCommented("ru"),
            TaskApprovalRequested("en"),
            TaskApprovalRequested("tr"),
            TaskApprovalRequested("fr"),
            TaskApprovalRequested("es"),
            TaskApprovalRequested("zh"),
            TaskApprovalRequested("ar"),
            TaskApprovalRequested("ru")
        ];
    }

    private static NotificationTemplate TenantInvite(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.invite.email",
            locale,
            isTurkish ? "Diten tenant davetiniz" : "Your Diten tenant invitation",
            isTurkish
                ? "<p>Merhaba,</p><p>{{TenantDisplayName}} tenant ortamina davet edildiniz.</p><p>Tenant Id: {{TenantId}}</p>"
                : "<p>Hello,</p><p>You have been invited to the {{TenantDisplayName}} tenant.</p><p>Tenant Id: {{TenantId}}</p>",
            isTurkish
                ? "Merhaba, {{TenantDisplayName}} tenant ortamina davet edildiniz. Tenant Id: {{TenantId}}"
                : "Hello, you have been invited to the {{TenantDisplayName}} tenant. Tenant Id: {{TenantId}}",
            ["TenantId", "TenantDisplayName"]);
    }

    private static NotificationTemplate TenantSuspended(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.suspended.email",
            locale,
            isTurkish ? "Diten tenant erisimi askiya alindi" : "Diten tenant access suspended",
            isTurkish
                ? "<p>Merhaba,</p><p>Tenant erisiminiz askiya alindi.</p><p>Neden: {{Reason}}</p><p>Tarih: {{SuspendedAtUtc}}</p>"
                : "<p>Hello,</p><p>Your tenant access has been suspended.</p><p>Reason: {{Reason}}</p><p>Date: {{SuspendedAtUtc}}</p>",
            isTurkish
                ? "Tenant erisiminiz askiya alindi. Neden: {{Reason}} Tarih: {{SuspendedAtUtc}}"
                : "Your tenant access has been suspended. Reason: {{Reason}} Date: {{SuspendedAtUtc}}",
            ["Reason", "SuspendedAtUtc"]);
    }

    private static NotificationTemplate TenantReactivated(string locale)
    {
        var isTurkish = locale == "tr";
        return Create(
            "tenant.reactivated.email",
            locale,
            isTurkish ? "Diten tenant erisimi yeniden acildi" : "Diten tenant access reactivated",
            isTurkish
                ? "<p>Merhaba,</p><p>Tenant erisiminiz yeniden acildi.</p><p>Tarih: {{ReactivatedAtUtc}}</p>"
                : "<p>Hello,</p><p>Your tenant access has been reactivated.</p><p>Date: {{ReactivatedAtUtc}}</p>",
            isTurkish
                ? "Tenant erisiminiz yeniden acildi. Tarih: {{ReactivatedAtUtc}}"
                : "Your tenant access has been reactivated. Date: {{ReactivatedAtUtc}}",
            ["ReactivatedAtUtc"]);
    }

    /// <summary>
    /// <c>platform.tasks.assigned</c> in seven languages. The required variables match the manifest's declaration
    /// exactly — a template that renders a variable the event does not supply produces a silent blank, which is
    /// the kind of defect nobody reports because the email still "arrived".
    /// </summary>
    private static NotificationTemplate TaskAssigned(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task was assigned to you: {{TaskTitle}}", "<p>A task has been assigned to you.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p><p>Due date: {{DueAt}}</p>", "A task has been assigned to you. Task: {{TaskTitle}} — Reference: {{TaskId}} — Due date: {{DueAt}}"),
            "tr" => ("Size bir görev atandı: {{TaskTitle}}", "<p>Size bir görev atandı.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p><p>Son tarih: {{DueAt}}</p>", "Size bir görev atandı. Görev: {{TaskTitle}} — Referans: {{TaskId}} — Son tarih: {{DueAt}}"),
            "fr" => ("Une tâche vous a été attribuée : {{TaskTitle}}", "<p>Une tâche vous a été attribuée.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p><p>Échéance: {{DueAt}}</p>", "Une tâche vous a été attribuée. Tâche: {{TaskTitle}} — Référence: {{TaskId}} — Échéance: {{DueAt}}"),
            "es" => ("Se le ha asignado una tarea: {{TaskTitle}}", "<p>Se le ha asignado una tarea.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p><p>Fecha de vencimiento: {{DueAt}}</p>", "Se le ha asignado una tarea. Tarea: {{TaskTitle}} — Referencia: {{TaskId}} — Fecha de vencimiento: {{DueAt}}"),
            "zh" => ("有任务分配给您：{{TaskTitle}}", "<p>有一项任务已分配给您。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p><p>截止日期: {{DueAt}}</p>", "有一项任务已分配给您。 任务: {{TaskTitle}} — 编号: {{TaskId}} — 截止日期: {{DueAt}}"),
            "ar" => ("تم إسناد مهمة إليك: {{TaskTitle}}", "<p>تم إسناد مهمة إليك.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p><p>تاريخ الاستحقاق: {{DueAt}}</p>", "تم إسناد مهمة إليك. المهمة: {{TaskTitle}} — المرجع: {{TaskId}} — تاريخ الاستحقاق: {{DueAt}}"),
            "ru" => ("Вам назначена задача: {{TaskTitle}}", "<p>Вам назначена задача.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p><p>Срок: {{DueAt}}</p>", "Вам назначена задача. Задача: {{TaskTitle}} — Ссылка: {{TaskId}} — Срок: {{DueAt}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.assigned", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.claimed</c> in seven languages. The required variables match the manifest's declaration
    /// exactly — a template that renders a variable the event does not supply produces a silent blank, which is
    /// the kind of defect nobody reports because the email still "arrived".
    /// </summary>
    private static NotificationTemplate TaskClaimed(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task you requested was claimed: {{TaskTitle}}", "<p>Someone has taken on a task you requested.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "Someone has taken on a task you requested. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("İstediğiniz görev üstlenildi: {{TaskTitle}}", "<p>Talep ettiğiniz bir görevi biri üstlendi.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Talep ettiğiniz bir görevi biri üstlendi. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Une tâche que vous avez demandée a été prise en charge : {{TaskTitle}}", "<p>Quelqu’un a pris en charge une tâche que vous avez demandée.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Quelqu’un a pris en charge une tâche que vous avez demandée. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Una tarea que solicitó fue tomada: {{TaskTitle}}", "<p>Alguien ha asumido una tarea que usted solicitó.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Alguien ha asumido una tarea que usted solicitó. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("您请求的任务已被认领：{{TaskTitle}}", "<p>有人已认领您请求的任务。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "有人已认领您请求的任务。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("تم استلام مهمة طلبتها: {{TaskTitle}}", "<p>قام أحدهم باستلام مهمة طلبتها.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "قام أحدهم باستلام مهمة طلبتها. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Запрошенную вами задачу взяли в работу: {{TaskTitle}}", "<p>Кто-то взял в работу задачу, которую вы запросили.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Кто-то взял в работу задачу, которую вы запросили. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.claimed", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.duesoon</c> in seven languages. The required variables match the manifest's declaration
    /// exactly — a template that renders a variable the event does not supply produces a silent blank, which is
    /// the kind of defect nobody reports because the email still "arrived".
    /// </summary>
    private static NotificationTemplate TaskDueSoon(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task is due soon: {{TaskTitle}}", "<p>A task you hold is approaching its due date.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p><p>Due date: {{DueAt}}</p>", "A task you hold is approaching its due date. Task: {{TaskTitle}} — Reference: {{TaskId}} — Due date: {{DueAt}}"),
            "tr" => ("Bir görevin süresi yaklaşıyor: {{TaskTitle}}", "<p>Üzerinizdeki bir görevin son tarihi yaklaşıyor.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p><p>Son tarih: {{DueAt}}</p>", "Üzerinizdeki bir görevin son tarihi yaklaşıyor. Görev: {{TaskTitle}} — Referans: {{TaskId}} — Son tarih: {{DueAt}}"),
            "fr" => ("Une tâche arrive à échéance : {{TaskTitle}}", "<p>Une tâche dont vous avez la charge approche de son échéance.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p><p>Échéance: {{DueAt}}</p>", "Une tâche dont vous avez la charge approche de son échéance. Tâche: {{TaskTitle}} — Référence: {{TaskId}} — Échéance: {{DueAt}}"),
            "es" => ("Una tarea vence pronto: {{TaskTitle}}", "<p>Una tarea a su cargo se acerca a su fecha de vencimiento.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p><p>Fecha de vencimiento: {{DueAt}}</p>", "Una tarea a su cargo se acerca a su fecha de vencimiento. Tarea: {{TaskTitle}} — Referencia: {{TaskId}} — Fecha de vencimiento: {{DueAt}}"),
            "zh" => ("任务即将到期：{{TaskTitle}}", "<p>您负责的一项任务即将到期。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p><p>截止日期: {{DueAt}}</p>", "您负责的一项任务即将到期。 任务: {{TaskTitle}} — 编号: {{TaskId}} — 截止日期: {{DueAt}}"),
            "ar" => ("مهمة تقترب من موعدها: {{TaskTitle}}", "<p>مهمة لديك تقترب من تاريخ استحقاقها.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p><p>تاريخ الاستحقاق: {{DueAt}}</p>", "مهمة لديك تقترب من تاريخ استحقاقها. المهمة: {{TaskTitle}} — المرجع: {{TaskId}} — تاريخ الاستحقاق: {{DueAt}}"),
            "ru" => ("Срок задачи подходит: {{TaskTitle}}", "<p>У задачи, которая за вами закреплена, приближается срок.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p><p>Срок: {{DueAt}}</p>", "У задачи, которая за вами закреплена, приближается срок. Задача: {{TaskTitle}} — Ссылка: {{TaskId}} — Срок: {{DueAt}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.duesoon", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.completed</c> in seven languages. The required variables match the manifest's declaration
    /// exactly — a template that renders a variable the event does not supply produces a silent blank, which is
    /// the kind of defect nobody reports because the email still "arrived".
    /// </summary>
    private static NotificationTemplate TaskCompleted(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task you requested is complete: {{TaskTitle}}", "<p>A task you requested has been completed.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "A task you requested has been completed. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("İstediğiniz görev tamamlandı: {{TaskTitle}}", "<p>Talep ettiğiniz bir görev tamamlandı.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Talep ettiğiniz bir görev tamamlandı. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Une tâche que vous avez demandée est terminée : {{TaskTitle}}", "<p>Une tâche que vous avez demandée a été terminée.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Une tâche que vous avez demandée a été terminée. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Una tarea que solicitó está completa: {{TaskTitle}}", "<p>Se ha completado una tarea que usted solicitó.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Se ha completado una tarea que usted solicitó. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("您请求的任务已完成：{{TaskTitle}}", "<p>您请求的任务已完成。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "您请求的任务已完成。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("اكتملت مهمة طلبتها: {{TaskTitle}}", "<p>اكتملت مهمة كنت قد طلبتها.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "اكتملت مهمة كنت قد طلبتها. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Запрошенная вами задача выполнена: {{TaskTitle}}", "<p>Запрошенная вами задача выполнена.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Запрошенная вами задача выполнена. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.completed", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.commented</c> in seven languages.
    ///
    /// <para>Sent for a NEW comment only. An edit and a withdrawal deliberately send nothing (owner decision
    /// 2026-08-14): a typo correction does not earn anybody's inbox, and a retraction that emailed everyone would
    /// shout louder than the sentence it takes back.</para>
    ///
    /// <para>The body carries the task, NOT the comment text. Two reasons, and the second is the load-bearing
    /// one: a comment can be withdrawn, and an email is unrecallable — quoting the sentence would put a copy of
    /// it beyond the reach of the retraction the author is entitled to make. It also keeps the template's
    /// variables identical to its five siblings, which is what lets the manifest declare one variable set.</para>
    /// </summary>
    private static NotificationTemplate TaskCommented(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("New comment on a task: {{TaskTitle}}", "<p>Somebody commented on a task you are involved in.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "Somebody commented on a task you are involved in. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("Bir göreve yeni yorum: {{TaskTitle}}", "<p>İlgili olduğunuz bir göreve yorum yazıldı.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "İlgili olduğunuz bir göreve yorum yazıldı. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Nouveau commentaire sur une tâche : {{TaskTitle}}", "<p>Quelqu\u0027un a commenté une tâche qui vous concerne.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Quelqu\u0027un a commenté une tâche qui vous concerne. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Nuevo comentario en una tarea: {{TaskTitle}}", "<p>Alguien comentó en una tarea en la que usted participa.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Alguien comentó en una tarea en la que usted participa. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("任务有新评论：{{TaskTitle}}", "<p>有人在您参与的任务上发表了评论。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "有人在您参与的任务上发表了评论。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("تعليق جديد على مهمة: {{TaskTitle}}", "<p>علّق أحدهم على مهمة تخصك.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "علّق أحدهم على مهمة تخصك. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Новый комментарий к задаче: {{TaskTitle}}", "<p>Кто-то оставил комментарий к задаче, которая вас касается.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Кто-то оставил комментарий к задаче, которая вас касается. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.commented", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.approvalrequested</c> in seven languages. The required variables match the manifest's declaration
    /// exactly — a template that renders a variable the event does not supply produces a silent blank, which is
    /// the kind of defect nobody reports because the email still "arrived".
    /// </summary>
    private static NotificationTemplate TaskApprovalRequested(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task needs your approval: {{TaskTitle}}", "<p>A task is waiting for your approval before work may begin.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "A task is waiting for your approval before work may begin. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("Bir görev onayınızı bekliyor: {{TaskTitle}}", "<p>Bir görev, çalışma başlamadan önce onayınızı bekliyor.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Bir görev, çalışma başlamadan önce onayınızı bekliyor. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Une tâche attend votre approbation : {{TaskTitle}}", "<p>Une tâche attend votre approbation avant que le travail puisse commencer.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Une tâche attend votre approbation avant que le travail puisse commencer. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Una tarea requiere su aprobación: {{TaskTitle}}", "<p>Una tarea espera su aprobación antes de que pueda comenzar el trabajo.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Una tarea espera su aprobación antes de que pueda comenzar el trabajo. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("有任务待您审批：{{TaskTitle}}", "<p>一项任务在开始工作前需要您的审批。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "一项任务在开始工作前需要您的审批。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("مهمة بانتظار موافقتك: {{TaskTitle}}", "<p>مهمة تنتظر موافقتك قبل أن يبدأ العمل.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "مهمة تنتظر موافقتك قبل أن يبدأ العمل. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Задача ожидает вашего согласования: {{TaskTitle}}", "<p>Задача ожидает вашего согласования, прежде чем работа начнётся.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Задача ожидает вашего согласования, прежде чем работа начнётся. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.approvalrequested", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    private static NotificationTemplate Create(
        string templateKey,
        string locale,
        string subject,
        string bodyHtml,
        string bodyText,
        IReadOnlyList<string> requiredVariables)
    {
        return new NotificationTemplate
        {
            TenantId = null,
            IsPlatformDefault = true,
            TemplateKey = templateKey,
            Channel = NotificationChannelCode.Email,
            Locale = locale,
            SubjectTemplate = subject,
            BodyHtmlTemplate = bodyHtml,
            BodyTextTemplate = bodyText,
            Variables = requiredVariables
                .Select(name => new TemplateVariableDefinition
                {
                    Name = name,
                    Type = TemplateVariableType.String,
                    IsRequired = true
                })
                .ToList(),
            Status = NotificationTemplateStatus.Active,
            SemanticVersion = "1.0.0",
            CreatedBy = "system",
            Version = 1,
            IsDeleted = false
        };
    }
}
