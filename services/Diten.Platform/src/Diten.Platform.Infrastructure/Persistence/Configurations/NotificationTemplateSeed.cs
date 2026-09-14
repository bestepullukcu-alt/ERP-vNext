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
            TaskMentioned("en"),
            TaskMentioned("tr"),
            TaskMentioned("fr"),
            TaskMentioned("es"),
            TaskMentioned("zh"),
            TaskMentioned("ar"),
            TaskMentioned("ru"),
            TaskApprovalRequested("en"),
            TaskApprovalRequested("tr"),
            TaskApprovalRequested("fr"),
            TaskApprovalRequested("es"),
            TaskApprovalRequested("zh"),
            TaskApprovalRequested("ar"),
            TaskApprovalRequested("ru"),

            /*
             * MOD-0357 S5 meeting notifications, SEVEN languages each — the same reason MOD-0024's task events
             * are: a tenant surface ships all seven, or two of them show an English e-mail to five sets of
             * readers who never asked for one.
             */
            MeetingInvite("en"),
            MeetingInvite("tr"),
            MeetingInvite("fr"),
            MeetingInvite("es"),
            MeetingInvite("zh"),
            MeetingInvite("ar"),
            MeetingInvite("ru"),
            MeetingChange("en"),
            MeetingChange("tr"),
            MeetingChange("fr"),
            MeetingChange("es"),
            MeetingChange("zh"),
            MeetingChange("ar"),
            MeetingChange("ru"),
            MeetingCancel("en"),
            MeetingCancel("tr"),
            MeetingCancel("fr"),
            MeetingCancel("es"),
            MeetingCancel("zh"),
            MeetingCancel("ar"),
            MeetingCancel("ru"),
            MeetingRemoved("en"),
            MeetingRemoved("tr"),
            MeetingRemoved("fr"),
            MeetingRemoved("es"),
            MeetingRemoved("zh"),
            MeetingRemoved("ar"),
            MeetingRemoved("ru")
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
    /// <c>platform.tasks.mentioned</c> in seven languages (WP-PSS-MOD0024-TASK-MENTIONS-01).
    ///
    /// <para>Distinct from <c>platform.tasks.commented</c>: a direct @mention, not "you're involved in this
    /// conversation". <c>AddTaskCommentHandler</c> excludes the mentioned person from the Commented audience so
    /// the two never both fire for the same comment.</para>
    ///
    /// <para>Same reason as its siblings for NOT carrying the comment text: a comment can be withdrawn and an
    /// email cannot be recalled, so quoting a retractable sentence into an unrecallable one is precisely the
    /// thing this event must not do.</para>
    /// </summary>
    private static NotificationTemplate TaskMentioned(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("You were mentioned on a task: {{TaskTitle}}", "<p>Somebody mentioned you in a comment on a task.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "Somebody mentioned you in a comment on a task. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("Bir görevde sizden bahsedildi: {{TaskTitle}}", "<p>Bir göreve yazılan yorumda sizden bahsedildi.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Bir göreve yazılan yorumda sizden bahsedildi. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Vous avez été mentionné sur une tâche : {{TaskTitle}}", "<p>Quelqu'un vous a mentionné dans un commentaire sur une tâche.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Quelqu'un vous a mentionné dans un commentaire sur une tâche. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Le mencionaron en una tarea: {{TaskTitle}}", "<p>Alguien le mencionó en un comentario de una tarea.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Alguien le mencionó en un comentario de una tarea. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("有人在任务中提到了您：{{TaskTitle}}", "<p>有人在任务的评论中提到了您。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "有人在任务的评论中提到了您。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("تمت الإشارة إليك في مهمة: {{TaskTitle}}", "<p>أشار أحدهم إليك في تعليق على مهمة.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "أشار أحدهم إليك في تعليق على مهمة. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Вас упомянули в задаче: {{TaskTitle}}", "<p>Кто-то упомянул вас в комментарии к задаче.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Кто-то упомянул вас в комментарии к задаче. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.mentioned", locale, subject, html, text, ["TaskTitle", "TaskId"]);
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

    /// <summary>
    /// <c>platform.meetings.invite</c> in seven languages (MOD-0357 S5). <c>{{Location}}</c> is interpolated
    /// but NOT in the required list below — MeetingManifestProvider declares it optional, matching
    /// <c>TaskAssigned</c>'s own <c>DueAt</c> precedent: an absent optional variable renders as an empty string.
    /// </summary>
    private static NotificationTemplate MeetingInvite(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("You're invited: {{MeetingTitle}}", "<p>You have been invited to a meeting.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>When: {{StartAt}} – {{EndAt}}</p><p>Organizer: {{Organizer}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Open the meeting</a></p>", "You have been invited to a meeting. Meeting: {{MeetingTitle}} ({{MeetingType}}) — When: {{StartAt}} – {{EndAt}} — Organizer: {{Organizer}} — Location: {{Location}} — Open it: {{MeetingUrl}}"),
            "tr" => ("Davet edildiniz: {{MeetingTitle}}", "<p>Bir toplantıya davet edildiniz.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Zaman: {{StartAt}} – {{EndAt}}</p><p>Organizatör: {{Organizer}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı aç</a></p>", "Bir toplantıya davet edildiniz. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Zaman: {{StartAt}} – {{EndAt}} — Organizatör: {{Organizer}} — Konum: {{Location}} — Aç: {{MeetingUrl}}"),
            "fr" => ("Vous êtes invité : {{MeetingTitle}}", "<p>Vous avez été invité à une réunion.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Quand: {{StartAt}} – {{EndAt}}</p><p>Organisateur: {{Organizer}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ouvrir la réunion</a></p>", "Vous avez été invité à une réunion. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Quand: {{StartAt}} – {{EndAt}} — Organisateur: {{Organizer}} — Lieu: {{Location}} — Ouvrir: {{MeetingUrl}}"),
            "es" => ("Ha sido invitado: {{MeetingTitle}}", "<p>Ha sido invitado a una reunión.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Cuándo: {{StartAt}} – {{EndAt}}</p><p>Organizador: {{Organizer}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Abrir la reunión</a></p>", "Ha sido invitado a una reunión. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Cuándo: {{StartAt}} – {{EndAt}} — Organizador: {{Organizer}} — Lugar: {{Location}} — Abrir: {{MeetingUrl}}"),
            "zh" => ("您被邀请参加会议：{{MeetingTitle}}", "<p>您已被邀请参加一次会议。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>时间: {{StartAt}} – {{EndAt}}</p><p>组织者: {{Organizer}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">打开会议</a></p>", "您已被邀请参加一次会议。 会议: {{MeetingTitle}}（{{MeetingType}}） — 时间: {{StartAt}} – {{EndAt}} — 组织者: {{Organizer}} — 地点: {{Location}} — 打开: {{MeetingUrl}}"),
            "ar" => ("أنت مدعو: {{MeetingTitle}}", "<p>لقد تمت دعوتك إلى اجتماع.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>الوقت: {{StartAt}} – {{EndAt}}</p><p>المنظم: {{Organizer}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">فتح الاجتماع</a></p>", "لقد تمت دعوتك إلى اجتماع. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — الوقت: {{StartAt}} – {{EndAt}} — المنظم: {{Organizer}} — المكان: {{Location}} — فتح: {{MeetingUrl}}"),
            "ru" => ("Вас пригласили: {{MeetingTitle}}", "<p>Вас пригласили на совещание.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Когда: {{StartAt}} – {{EndAt}}</p><p>Организатор: {{Organizer}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Открыть совещание</a></p>", "Вас пригласили на совещание. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Когда: {{StartAt}} – {{EndAt}} — Организатор: {{Organizer}} — Место: {{Location}} — Открыть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.invite", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "Organizer", "MeetingUrl"]);
    }

    /// <summary><c>platform.meetings.change</c> in seven languages — sent only when Start/End/Location actually
    /// moved (UpdateMeetingHandler's own <c>scheduleChanged</c> gate); a title/description edit sends nothing.</summary>
    private static NotificationTemplate MeetingChange(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Meeting updated: {{MeetingTitle}}", "<p>A meeting you are invited to has changed.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>New time: {{StartAt}} – {{EndAt}}</p><p>Organizer: {{Organizer}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Open the meeting</a></p>", "A meeting you are invited to has changed. Meeting: {{MeetingTitle}} ({{MeetingType}}) — New time: {{StartAt}} – {{EndAt}} — Organizer: {{Organizer}} — Location: {{Location}} — Open it: {{MeetingUrl}}"),
            "tr" => ("Toplantı güncellendi: {{MeetingTitle}}", "<p>Davetli olduğunuz bir toplantı değişti.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Yeni zaman: {{StartAt}} – {{EndAt}}</p><p>Organizatör: {{Organizer}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı aç</a></p>", "Davetli olduğunuz bir toplantı değişti. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Yeni zaman: {{StartAt}} – {{EndAt}} — Organizatör: {{Organizer}} — Konum: {{Location}} — Aç: {{MeetingUrl}}"),
            "fr" => ("Réunion modifiée : {{MeetingTitle}}", "<p>Une réunion à laquelle vous êtes invité a changé.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Nouvel horaire: {{StartAt}} – {{EndAt}}</p><p>Organisateur: {{Organizer}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ouvrir la réunion</a></p>", "Une réunion à laquelle vous êtes invité a changé. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Nouvel horaire: {{StartAt}} – {{EndAt}} — Organisateur: {{Organizer}} — Lieu: {{Location}} — Ouvrir: {{MeetingUrl}}"),
            "es" => ("Reunión actualizada: {{MeetingTitle}}", "<p>Una reunión a la que está invitado ha cambiado.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Nuevo horario: {{StartAt}} – {{EndAt}}</p><p>Organizador: {{Organizer}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Abrir la reunión</a></p>", "Una reunión a la que está invitado ha cambiado. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Nuevo horario: {{StartAt}} – {{EndAt}} — Organizador: {{Organizer}} — Lugar: {{Location}} — Abrir: {{MeetingUrl}}"),
            "zh" => ("会议已更新：{{MeetingTitle}}", "<p>您受邀参加的一次会议已更改。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>新时间: {{StartAt}} – {{EndAt}}</p><p>组织者: {{Organizer}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">打开会议</a></p>", "您受邀参加的一次会议已更改。 会议: {{MeetingTitle}}（{{MeetingType}}） — 新时间: {{StartAt}} – {{EndAt}} — 组织者: {{Organizer}} — 地点: {{Location}} — 打开: {{MeetingUrl}}"),
            "ar" => ("تم تحديث الاجتماع: {{MeetingTitle}}", "<p>تغيّر اجتماع أنت مدعو إليه.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>الوقت الجديد: {{StartAt}} – {{EndAt}}</p><p>المنظم: {{Organizer}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">فتح الاجتماع</a></p>", "تغيّر اجتماع أنت مدعو إليه. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — الوقت الجديد: {{StartAt}} – {{EndAt}} — المنظم: {{Organizer}} — المكان: {{Location}} — فتح: {{MeetingUrl}}"),
            "ru" => ("Совещание изменено: {{MeetingTitle}}", "<p>Совещание, на которое вы приглашены, изменилось.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Новое время: {{StartAt}} – {{EndAt}}</p><p>Организатор: {{Organizer}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Открыть совещание</a></p>", "Совещание, на которое вы приглашены, изменилось. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Новое время: {{StartAt}} – {{EndAt}} — Организатор: {{Organizer}} — Место: {{Location}} — Открыть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.change", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "Organizer", "MeetingUrl"]);
    }

    /// <summary><c>platform.meetings.cancel</c> in seven languages — unlike <see cref="MeetingChange"/>, this
    /// ALWAYS sends (CancelMeetingHandler's own rule): there is no "not notice-worthy" cancellation.</summary>
    private static NotificationTemplate MeetingCancel(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Meeting cancelled: {{MeetingTitle}}", "<p>A meeting you were invited to has been cancelled.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Was scheduled: {{StartAt}} – {{EndAt}}</p><p>Organizer: {{Organizer}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">View the meeting</a></p>", "A meeting you were invited to has been cancelled. Meeting: {{MeetingTitle}} ({{MeetingType}}) — Was scheduled: {{StartAt}} – {{EndAt}} — Organizer: {{Organizer}} — Location: {{Location}} — View it: {{MeetingUrl}}"),
            "tr" => ("Toplantı iptal edildi: {{MeetingTitle}}", "<p>Davetli olduğunuz bir toplantı iptal edildi.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Planlanan zaman: {{StartAt}} – {{EndAt}}</p><p>Organizatör: {{Organizer}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı görüntüle</a></p>", "Davetli olduğunuz bir toplantı iptal edildi. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Planlanan zaman: {{StartAt}} – {{EndAt}} — Organizatör: {{Organizer}} — Konum: {{Location}} — Görüntüle: {{MeetingUrl}}"),
            "fr" => ("Réunion annulée : {{MeetingTitle}}", "<p>Une réunion à laquelle vous étiez invité a été annulée.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Était prévue: {{StartAt}} – {{EndAt}}</p><p>Organisateur: {{Organizer}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Voir la réunion</a></p>", "Une réunion à laquelle vous étiez invité a été annulée. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Était prévue: {{StartAt}} – {{EndAt}} — Organisateur: {{Organizer}} — Lieu: {{Location}} — Voir: {{MeetingUrl}}"),
            "es" => ("Reunión cancelada: {{MeetingTitle}}", "<p>Una reunión a la que estaba invitado ha sido cancelada.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Estaba programada: {{StartAt}} – {{EndAt}}</p><p>Organizador: {{Organizer}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ver la reunión</a></p>", "Una reunión a la que estaba invitado ha sido cancelada. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Estaba programada: {{StartAt}} – {{EndAt}} — Organizador: {{Organizer}} — Lugar: {{Location}} — Ver: {{MeetingUrl}}"),
            "zh" => ("会议已取消：{{MeetingTitle}}", "<p>您受邀参加的一次会议已被取消。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>原定时间: {{StartAt}} – {{EndAt}}</p><p>组织者: {{Organizer}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">查看会议</a></p>", "您受邀参加的一次会议已被取消。 会议: {{MeetingTitle}}（{{MeetingType}}） — 原定时间: {{StartAt}} – {{EndAt}} — 组织者: {{Organizer}} — 地点: {{Location}} — 查看: {{MeetingUrl}}"),
            "ar" => ("تم إلغاء الاجتماع: {{MeetingTitle}}", "<p>تم إلغاء اجتماع كنت مدعوًا إليه.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>كان مقررًا: {{StartAt}} – {{EndAt}}</p><p>المنظم: {{Organizer}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">عرض الاجتماع</a></p>", "تم إلغاء اجتماع كنت مدعوًا إليه. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — كان مقررًا: {{StartAt}} – {{EndAt}} — المنظم: {{Organizer}} — المكان: {{Location}} — عرض: {{MeetingUrl}}"),
            "ru" => ("Совещание отменено: {{MeetingTitle}}", "<p>Совещание, на которое вы были приглашены, отменено.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Было запланировано: {{StartAt}} – {{EndAt}}</p><p>Организатор: {{Organizer}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Посмотреть совещание</a></p>", "Совещание, на которое вы были приглашены, отменено. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Было запланировано: {{StartAt}} – {{EndAt}} — Организатор: {{Organizer}} — Место: {{Location}} — Посмотреть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.cancel", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "Organizer", "MeetingUrl"]);
    }

    /// <summary><c>platform.meetings.removed</c> in seven languages (BL-386) — sent to the ONE attendee taken off
    /// an otherwise-still-scheduled meeting, never the rest of the invitees. Deliberately LINKLESS: no
    /// <c>{{MeetingUrl}}</c> anywhere in this template (unlike its three siblings above) — the meeting detail page
    /// 404s for a reader no longer on the meeting (D3's own visibility rule), so a link here would just be a dead
    /// end. The copy also says the meeting itself was NOT cancelled — <see cref="MeetingCancel"/>'s own wording
    /// would be actively wrong here.</summary>
    private static NotificationTemplate MeetingRemoved(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("You were removed from a meeting: {{MeetingTitle}}", "<p>You are no longer an attendee of this meeting. It will be removed from your calendar; the meeting itself has not been cancelled.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Was scheduled: {{StartAt}} – {{EndAt}}</p><p>Organizer: {{Organizer}}</p><p>Location: {{Location}}</p>", "You are no longer an attendee of this meeting. It will be removed from your calendar; the meeting itself has not been cancelled. Meeting: {{MeetingTitle}} ({{MeetingType}}) — Was scheduled: {{StartAt}} – {{EndAt}} — Organizer: {{Organizer}} — Location: {{Location}}"),
            "tr" => ("Toplantıdan çıkarıldınız: {{MeetingTitle}}", "<p>Artık bu toplantının katılımcısı değilsiniz. Toplantı takviminizden kaldırılacak; toplantının kendisi iptal edilmedi.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Planlanan zaman: {{StartAt}} – {{EndAt}}</p><p>Organizatör: {{Organizer}}</p><p>Konum: {{Location}}</p>", "Artık bu toplantının katılımcısı değilsiniz. Toplantı takviminizden kaldırılacak; toplantının kendisi iptal edilmedi. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Planlanan zaman: {{StartAt}} – {{EndAt}} — Organizatör: {{Organizer}} — Konum: {{Location}}"),
            "fr" => ("Vous avez été retiré d'une réunion : {{MeetingTitle}}", "<p>Vous n'êtes plus participant à cette réunion. Elle sera retirée de votre calendrier ; la réunion elle-même n'a pas été annulée.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Était prévue: {{StartAt}} – {{EndAt}}</p><p>Organisateur: {{Organizer}}</p><p>Lieu: {{Location}}</p>", "Vous n'êtes plus participant à cette réunion. Elle sera retirée de votre calendrier ; la réunion elle-même n'a pas été annulée. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Était prévue: {{StartAt}} – {{EndAt}} — Organisateur: {{Organizer}} — Lieu: {{Location}}"),
            "es" => ("Se le ha eliminado de una reunión: {{MeetingTitle}}", "<p>Ya no es participante de esta reunión. Se eliminará de su calendario; la reunión en sí no ha sido cancelada.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Estaba programada: {{StartAt}} – {{EndAt}}</p><p>Organizador: {{Organizer}}</p><p>Lugar: {{Location}}</p>", "Ya no es participante de esta reunión. Se eliminará de su calendario; la reunión en sí no ha sido cancelada. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Estaba programada: {{StartAt}} – {{EndAt}} — Organizador: {{Organizer}} — Lugar: {{Location}}"),
            "zh" => ("您已被移出会议：{{MeetingTitle}}", "<p>您不再是此会议的参会者。该会议将从您的日历中移除；会议本身并未被取消。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>原定时间: {{StartAt}} – {{EndAt}}</p><p>组织者: {{Organizer}}</p><p>地点: {{Location}}</p>", "您不再是此会议的参会者。该会议将从您的日历中移除；会议本身并未被取消。 会议: {{MeetingTitle}}（{{MeetingType}}） — 原定时间: {{StartAt}} – {{EndAt}} — 组织者: {{Organizer}} — 地点: {{Location}}"),
            "ar" => ("تمت إزالتك من اجتماع: {{MeetingTitle}}", "<p>لم تعد مشاركًا في هذا الاجتماع. سيُزال من تقويمك؛ لم يُلغَ الاجتماع نفسه.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>كان مقررًا: {{StartAt}} – {{EndAt}}</p><p>المنظم: {{Organizer}}</p><p>المكان: {{Location}}</p>", "لم تعد مشاركًا في هذا الاجتماع. سيُزال من تقويمك؛ لم يُلغَ الاجتماع نفسه. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — كان مقررًا: {{StartAt}} – {{EndAt}} — المنظم: {{Organizer}} — المكان: {{Location}}"),
            "ru" => ("Вас исключили из совещания: {{MeetingTitle}}", "<p>Вы больше не являетесь участником этого совещания. Оно будет удалено из вашего календаря; само совещание не отменено.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Было запланировано: {{StartAt}} – {{EndAt}}</p><p>Организатор: {{Organizer}}</p><p>Место: {{Location}}</p>", "Вы больше не являетесь участником этого совещания. Оно будет удалено из вашего календаря; само совещание не отменено. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Было запланировано: {{StartAt}} – {{EndAt}} — Организатор: {{Organizer}} — Место: {{Location}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.removed", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "Organizer"]);
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
