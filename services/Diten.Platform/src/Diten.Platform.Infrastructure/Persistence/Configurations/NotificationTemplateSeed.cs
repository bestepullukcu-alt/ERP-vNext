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
            TaskInquiryAsked("en"),
            TaskInquiryAsked("tr"),
            TaskInquiryAsked("fr"),
            TaskInquiryAsked("es"),
            TaskInquiryAsked("zh"),
            TaskInquiryAsked("ar"),
            TaskInquiryAsked("ru"),
            TaskInquiryAnswered("en"),
            TaskInquiryAnswered("tr"),
            TaskInquiryAnswered("fr"),
            TaskInquiryAnswered("es"),
            TaskInquiryAnswered("zh"),
            TaskInquiryAnswered("ar"),
            TaskInquiryAnswered("ru"),
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
            MeetingRemoved("ru"),

            /*
             * MOD-0357 BL-387/BL-373 (owner, 2026-09-14) — the ORGANIZER's own variant of invite/change/cancel,
             * SEVEN languages each, same reason every other tenant-facing set here is: two of seven would show an
             * English e-mail to five sets of readers who never asked for one.
             */
            MeetingOrganizerAdded("en"),
            MeetingOrganizerAdded("tr"),
            MeetingOrganizerAdded("fr"),
            MeetingOrganizerAdded("es"),
            MeetingOrganizerAdded("zh"),
            MeetingOrganizerAdded("ar"),
            MeetingOrganizerAdded("ru"),
            MeetingOrganizerUpdated("en"),
            MeetingOrganizerUpdated("tr"),
            MeetingOrganizerUpdated("fr"),
            MeetingOrganizerUpdated("es"),
            MeetingOrganizerUpdated("zh"),
            MeetingOrganizerUpdated("ar"),
            MeetingOrganizerUpdated("ru"),
            MeetingOrganizerCancelled("en"),
            MeetingOrganizerCancelled("tr"),
            MeetingOrganizerCancelled("fr"),
            MeetingOrganizerCancelled("es"),
            MeetingOrganizerCancelled("zh"),
            MeetingOrganizerCancelled("ar"),
            MeetingOrganizerCancelled("ru"),

            /*
             * MOD-0280-FU01 T3 (pack §21.3 N7) — the time-entry events, SEVEN languages each: 7 × 7 = 49. The key is the
             * event code. Each template renders exactly the event's required variables (TimeEntryNotificationVariables),
             * in subject, HTML and text alike, and nothing else — so no placeholder is ever left blank. Minimal content:
             * a week and a link; an approver's e-mail names the person; nothing about anyone else, no minutes per day.
             */
            TimeEntryWeekSubmitted("en"),
            TimeEntryWeekSubmitted("tr"),
            TimeEntryWeekSubmitted("fr"),
            TimeEntryWeekSubmitted("es"),
            TimeEntryWeekSubmitted("zh"),
            TimeEntryWeekSubmitted("ar"),
            TimeEntryWeekSubmitted("ru"),
            TimeEntryWeekWithdrawn("en"),
            TimeEntryWeekWithdrawn("tr"),
            TimeEntryWeekWithdrawn("fr"),
            TimeEntryWeekWithdrawn("es"),
            TimeEntryWeekWithdrawn("zh"),
            TimeEntryWeekWithdrawn("ar"),
            TimeEntryWeekWithdrawn("ru"),
            TimeEntryWeekApproved("en"),
            TimeEntryWeekApproved("tr"),
            TimeEntryWeekApproved("fr"),
            TimeEntryWeekApproved("es"),
            TimeEntryWeekApproved("zh"),
            TimeEntryWeekApproved("ar"),
            TimeEntryWeekApproved("ru"),
            TimeEntryWeekRejected("en"),
            TimeEntryWeekRejected("tr"),
            TimeEntryWeekRejected("fr"),
            TimeEntryWeekRejected("es"),
            TimeEntryWeekRejected("zh"),
            TimeEntryWeekRejected("ar"),
            TimeEntryWeekRejected("ru"),
            TimeEntryWeekReminder("en"),
            TimeEntryWeekReminder("tr"),
            TimeEntryWeekReminder("fr"),
            TimeEntryWeekReminder("es"),
            TimeEntryWeekReminder("zh"),
            TimeEntryWeekReminder("ar"),
            TimeEntryWeekReminder("ru"),
            TimeEntryTimerAutoClosed("en"),
            TimeEntryTimerAutoClosed("tr"),
            TimeEntryTimerAutoClosed("fr"),
            TimeEntryTimerAutoClosed("es"),
            TimeEntryTimerAutoClosed("zh"),
            TimeEntryTimerAutoClosed("ar"),
            TimeEntryTimerAutoClosed("ru"),
            TimeEntryMinutesConflict("en"),
            TimeEntryMinutesConflict("tr"),
            TimeEntryMinutesConflict("fr"),
            TimeEntryMinutesConflict("es"),
            TimeEntryMinutesConflict("zh"),
            TimeEntryMinutesConflict("ar"),
            TimeEntryMinutesConflict("ru")
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
    /// <c>platform.tasks.inquiryasked</c> in seven languages (BL-439) — a task's holder parked it waiting on YOU.
    ///
    /// <para>The QUESTION TEXT is deliberately not a variable, for the reason the comment events give: it is the
    /// holder's own sentence about work the reader may not otherwise see, and an e-mail cannot be recalled or
    /// scoped. The reader opens the Task Center, where the read rule decides what they see.</para>
    /// </summary>
    private static NotificationTemplate TaskInquiryAsked(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("A task is waiting for your answer: {{TaskTitle}}", "<p>Somebody has a question for you about a task and cannot continue until you answer. The question is in your Task Center inbox.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "Somebody has a question for you about a task and cannot continue until you answer. The question is in your Task Center inbox. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("Bir görev cevabınızı bekliyor: {{TaskTitle}}", "<p>Bir görevle ilgili size bir soru soruldu; cevap gelene kadar iş ilerleyemiyor. Soru, Görev Merkezi'ndeki gelen kutunuzda.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Bir görevle ilgili size bir soru soruldu; cevap gelene kadar iş ilerleyemiyor. Soru, Görev Merkezi'ndeki gelen kutunuzda. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Une tâche attend votre réponse : {{TaskTitle}}", "<p>Quelqu'un vous pose une question sur une tâche et ne peut pas continuer sans votre réponse. La question se trouve dans la boîte de réception de votre Centre des tâches.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "Quelqu'un vous pose une question sur une tâche et ne peut pas continuer sans votre réponse. La question se trouve dans la boîte de réception de votre Centre des tâches. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Una tarea espera su respuesta: {{TaskTitle}}", "<p>Alguien le ha hecho una pregunta sobre una tarea y no puede continuar hasta que usted responda. La pregunta está en la bandeja de entrada de su Centro de tareas.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "Alguien le ha hecho una pregunta sobre una tarea y no puede continuar hasta que usted responda. La pregunta está en la bandeja de entrada de su Centro de tareas. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("有任务在等待您的回答：{{TaskTitle}}", "<p>有人就一项任务向您提问，在您回答之前工作无法继续。问题在您的任务中心收件箱中。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "有人就一项任务向您提问，在您回答之前工作无法继续。问题在您的任务中心收件箱中。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("مهمة بانتظار إجابتك: {{TaskTitle}}", "<p>لدى أحدهم سؤال لك بشأن مهمة ولا يمكنه المتابعة حتى تجيب. السؤال موجود في صندوق الوارد في مركز المهام.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "لدى أحدهم سؤال لك بشأن مهمة ولا يمكنه المتابعة حتى تجيب. السؤال موجود في صندوق الوارد في مركز المهام. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("Задача ждёт вашего ответа: {{TaskTitle}}", "<p>Вам задали вопрос по задаче, и работа не может продолжаться, пока вы не ответите. Вопрос находится во входящих вашего Центра задач.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Вам задали вопрос по задаче, и работа не может продолжаться, пока вы не ответите. Вопрос находится во входящих вашего Центра задач. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.inquiryasked", locale, subject, html, text, ["TaskTitle", "TaskId"]);
    }

    /// <summary>
    /// <c>platform.tasks.inquiryanswered</c> in seven languages (BL-439) — the question your task was parked on
    /// has been answered, and the task is back where it was. The ANSWER TEXT is not a variable, for the reason
    /// <see cref="TaskInquiryAsked"/> gives: it is in the task's history, behind the read rule.
    /// </summary>
    private static NotificationTemplate TaskInquiryAnswered(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your question was answered: {{TaskTitle}}", "<p>The person your task was waiting on has answered. The answer is in the task's history, and the task is back where it was before you asked.</p><p><strong>Task:</strong> {{TaskTitle}}</p><p>Reference: {{TaskId}}</p>", "The person your task was waiting on has answered. The answer is in the task's history, and the task is back where it was before you asked. Task: {{TaskTitle}} — Reference: {{TaskId}}"),
            "tr" => ("Sorunuz cevaplandı: {{TaskTitle}}", "<p>Görevinizin beklediği kişi cevap verdi. Cevap görevin geçmişinde; görev, siz sormadan önceki durumuna döndü.</p><p><strong>Görev:</strong> {{TaskTitle}}</p><p>Referans: {{TaskId}}</p>", "Görevinizin beklediği kişi cevap verdi. Cevap görevin geçmişinde; görev, siz sormadan önceki durumuna döndü. Görev: {{TaskTitle}} — Referans: {{TaskId}}"),
            "fr" => ("Votre question a reçu une réponse : {{TaskTitle}}", "<p>La personne que votre tâche attendait a répondu. La réponse figure dans l'historique de la tâche, et la tâche est revenue à l'état où elle était avant votre question.</p><p><strong>Tâche:</strong> {{TaskTitle}}</p><p>Référence: {{TaskId}}</p>", "La personne que votre tâche attendait a répondu. La réponse figure dans l'historique de la tâche, et la tâche est revenue à l'état où elle était avant votre question. Tâche: {{TaskTitle}} — Référence: {{TaskId}}"),
            "es" => ("Su pregunta ha sido respondida: {{TaskTitle}}", "<p>La persona a la que esperaba su tarea ha respondido. La respuesta está en el historial de la tarea, y la tarea ha vuelto al estado en que estaba antes de su pregunta.</p><p><strong>Tarea:</strong> {{TaskTitle}}</p><p>Referencia: {{TaskId}}</p>", "La persona a la que esperaba su tarea ha respondido. La respuesta está en el historial de la tarea, y la tarea ha vuelto al estado en que estaba antes de su pregunta. Tarea: {{TaskTitle}} — Referencia: {{TaskId}}"),
            "zh" => ("您的问题已得到回答：{{TaskTitle}}", "<p>您的任务所等待的人已经回答。答案记录在任务的历史中，任务已恢复到您提问之前的状态。</p><p><strong>任务:</strong> {{TaskTitle}}</p><p>编号: {{TaskId}}</p>", "您的任务所等待的人已经回答。答案记录在任务的历史中，任务已恢复到您提问之前的状态。 任务: {{TaskTitle}} — 编号: {{TaskId}}"),
            "ar" => ("تمت الإجابة عن سؤالك: {{TaskTitle}}", "<p>أجاب الشخص الذي كانت مهمتك بانتظاره. الإجابة موجودة في سجل المهمة، وعادت المهمة إلى حالتها قبل أن تسأل.</p><p><strong>المهمة:</strong> {{TaskTitle}}</p><p>المرجع: {{TaskId}}</p>", "أجاب الشخص الذي كانت مهمتك بانتظاره. الإجابة موجودة في سجل المهمة، وعادت المهمة إلى حالتها قبل أن تسأل. المهمة: {{TaskTitle}} — المرجع: {{TaskId}}"),
            "ru" => ("На ваш вопрос ответили: {{TaskTitle}}", "<p>Человек, которого ждала ваша задача, ответил. Ответ сохранён в истории задачи, а задача вернулась в состояние, в котором была до вашего вопроса.</p><p><strong>Задача:</strong> {{TaskTitle}}</p><p>Ссылка: {{TaskId}}</p>", "Человек, которого ждала ваша задача, ответил. Ответ сохранён в истории задачи, а задача вернулась в состояние, в котором была до вашего вопроса. Задача: {{TaskTitle}} — Ссылка: {{TaskId}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported task template locale.")
        };

        return Create("platform.tasks.inquiryanswered", locale, subject, html, text, ["TaskTitle", "TaskId"]);
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

    /// <summary><c>platform.meetings.organizer-added</c> in seven languages (BL-387/BL-373) — the organizer's own
    /// reading of <see cref="MeetingInvite"/>: this meeting is on their calendar not because they were invited,
    /// but because it is theirs to run and someone/something else (a series sweep, a delegate) just scheduled it.
    /// No <c>{{Organizer}}</c> token — telling the reader they are the organizer of their own meeting is
    /// redundant, unlike the three sibling events above.</summary>
    private static NotificationTemplate MeetingOrganizerAdded(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Added to your calendar: {{MeetingTitle}}", "<p>A meeting you organize has been scheduled and added to your calendar.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>When: {{StartAt}} – {{EndAt}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Open the meeting</a></p>", "A meeting you organize has been scheduled and added to your calendar. Meeting: {{MeetingTitle}} ({{MeetingType}}) — When: {{StartAt}} – {{EndAt}} — Location: {{Location}} — Open it: {{MeetingUrl}}"),
            "tr" => ("Takviminize eklendi: {{MeetingTitle}}", "<p>Düzenleyeni olduğunuz bir toplantı planlandı ve takviminize eklendi.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Zaman: {{StartAt}} – {{EndAt}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı aç</a></p>", "Düzenleyeni olduğunuz bir toplantı planlandı ve takviminize eklendi. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Zaman: {{StartAt}} – {{EndAt}} — Konum: {{Location}} — Aç: {{MeetingUrl}}"),
            "fr" => ("Ajouté à votre calendrier : {{MeetingTitle}}", "<p>Une réunion que vous organisez a été planifiée et ajoutée à votre calendrier.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Quand: {{StartAt}} – {{EndAt}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ouvrir la réunion</a></p>", "Une réunion que vous organisez a été planifiée et ajoutée à votre calendrier. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Quand: {{StartAt}} – {{EndAt}} — Lieu: {{Location}} — Ouvrir: {{MeetingUrl}}"),
            "es" => ("Añadido a su calendario: {{MeetingTitle}}", "<p>Se ha programado una reunión que usted organiza y se ha añadido a su calendario.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Cuándo: {{StartAt}} – {{EndAt}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Abrir la reunión</a></p>", "Se ha programado una reunión que usted organiza y se ha añadido a su calendario. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Cuándo: {{StartAt}} – {{EndAt}} — Lugar: {{Location}} — Abrir: {{MeetingUrl}}"),
            "zh" => ("已添加到您的日历：{{MeetingTitle}}", "<p>您组织的一次会议已安排，并已添加到您的日历。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>时间: {{StartAt}} – {{EndAt}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">打开会议</a></p>", "您组织的一次会议已安排，并已添加到您的日历。 会议: {{MeetingTitle}}（{{MeetingType}}） — 时间: {{StartAt}} – {{EndAt}} — 地点: {{Location}} — 打开: {{MeetingUrl}}"),
            "ar" => ("أُضيف إلى تقويمك: {{MeetingTitle}}", "<p>تمت جدولة اجتماع تنظمه وأُضيف إلى تقويمك.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>الوقت: {{StartAt}} – {{EndAt}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">فتح الاجتماع</a></p>", "تمت جدولة اجتماع تنظمه وأُضيف إلى تقويمك. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — الوقت: {{StartAt}} – {{EndAt}} — المكان: {{Location}} — فتح: {{MeetingUrl}}"),
            "ru" => ("Добавлено в ваш календарь: {{MeetingTitle}}", "<p>Совещание, которое вы организуете, запланировано и добавлено в ваш календарь.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Когда: {{StartAt}} – {{EndAt}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Открыть совещание</a></p>", "Совещание, которое вы организуете, запланировано и добавлено в ваш календарь. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Когда: {{StartAt}} – {{EndAt}} — Место: {{Location}} — Открыть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.organizer-added", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "MeetingUrl"]);
    }

    /// <summary><c>platform.meetings.organizer-updated</c> in seven languages (BL-387) — the organizer's own
    /// reading of <see cref="MeetingChange"/>, sent only when someone OTHER than the organizer moved the
    /// meeting (the mailer's own organizer/others split, never a second <c>scheduleChanged</c> gate here).</summary>
    private static NotificationTemplate MeetingOrganizerUpdated(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your meeting was updated: {{MeetingTitle}}", "<p>A meeting you organize has changed.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>New time: {{StartAt}} – {{EndAt}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Open the meeting</a></p>", "A meeting you organize has changed. Meeting: {{MeetingTitle}} ({{MeetingType}}) — New time: {{StartAt}} – {{EndAt}} — Location: {{Location}} — Open it: {{MeetingUrl}}"),
            "tr" => ("Toplantınız güncellendi: {{MeetingTitle}}", "<p>Düzenleyeni olduğunuz bir toplantı değişti.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Yeni zaman: {{StartAt}} – {{EndAt}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı aç</a></p>", "Düzenleyeni olduğunuz bir toplantı değişti. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Yeni zaman: {{StartAt}} – {{EndAt}} — Konum: {{Location}} — Aç: {{MeetingUrl}}"),
            "fr" => ("Votre réunion a été modifiée : {{MeetingTitle}}", "<p>Une réunion que vous organisez a changé.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Nouvel horaire: {{StartAt}} – {{EndAt}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ouvrir la réunion</a></p>", "Une réunion que vous organisez a changé. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Nouvel horaire: {{StartAt}} – {{EndAt}} — Lieu: {{Location}} — Ouvrir: {{MeetingUrl}}"),
            "es" => ("Su reunión fue actualizada: {{MeetingTitle}}", "<p>Una reunión que usted organiza ha cambiado.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Nuevo horario: {{StartAt}} – {{EndAt}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Abrir la reunión</a></p>", "Una reunión que usted organiza ha cambiado. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Nuevo horario: {{StartAt}} – {{EndAt}} — Lugar: {{Location}} — Abrir: {{MeetingUrl}}"),
            "zh" => ("您的会议已更新：{{MeetingTitle}}", "<p>您组织的一次会议已更改。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>新时间: {{StartAt}} – {{EndAt}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">打开会议</a></p>", "您组织的一次会议已更改。 会议: {{MeetingTitle}}（{{MeetingType}}） — 新时间: {{StartAt}} – {{EndAt}} — 地点: {{Location}} — 打开: {{MeetingUrl}}"),
            "ar" => ("تم تحديث اجتماعك: {{MeetingTitle}}", "<p>تغيّر اجتماع تنظمه.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>الوقت الجديد: {{StartAt}} – {{EndAt}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">فتح الاجتماع</a></p>", "تغيّر اجتماع تنظمه. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — الوقت الجديد: {{StartAt}} – {{EndAt}} — المكان: {{Location}} — فتح: {{MeetingUrl}}"),
            "ru" => ("Ваше совещание обновлено: {{MeetingTitle}}", "<p>Совещание, которое вы организуете, изменилось.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Новое время: {{StartAt}} – {{EndAt}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Открыть совещание</a></p>", "Совещание, которое вы организуете, изменилось. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Новое время: {{StartAt}} – {{EndAt}} — Место: {{Location}} — Открыть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.organizer-updated", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "MeetingUrl"]);
    }

    /// <summary><c>platform.meetings.organizer-cancelled</c> in seven languages (BL-387) — the organizer's own
    /// reading of <see cref="MeetingCancel"/>, sent only when someone OTHER than the organizer cancelled the
    /// meeting. Keeps the link (unlike <see cref="MeetingRemoved"/>): D3's own visibility rule lets the
    /// organizer view a cancelled meeting they organized, so the link is not a dead end here.</summary>
    private static NotificationTemplate MeetingOrganizerCancelled(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your meeting was cancelled: {{MeetingTitle}}", "<p>A meeting you organize has been cancelled.</p><p><strong>Meeting:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Was scheduled: {{StartAt}} – {{EndAt}}</p><p>Location: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">View the meeting</a></p>", "A meeting you organize has been cancelled. Meeting: {{MeetingTitle}} ({{MeetingType}}) — Was scheduled: {{StartAt}} – {{EndAt}} — Location: {{Location}} — View it: {{MeetingUrl}}"),
            "tr" => ("Toplantınız iptal edildi: {{MeetingTitle}}", "<p>Düzenleyeni olduğunuz bir toplantı iptal edildi.</p><p><strong>Toplantı:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Planlanan zaman: {{StartAt}} – {{EndAt}}</p><p>Konum: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Toplantıyı görüntüle</a></p>", "Düzenleyeni olduğunuz bir toplantı iptal edildi. Toplantı: {{MeetingTitle}} ({{MeetingType}}) — Planlanan zaman: {{StartAt}} – {{EndAt}} — Konum: {{Location}} — Görüntüle: {{MeetingUrl}}"),
            "fr" => ("Votre réunion a été annulée : {{MeetingTitle}}", "<p>Une réunion que vous organisez a été annulée.</p><p><strong>Réunion:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Était prévue: {{StartAt}} – {{EndAt}}</p><p>Lieu: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Voir la réunion</a></p>", "Une réunion que vous organisez a été annulée. Réunion: {{MeetingTitle}} ({{MeetingType}}) — Était prévue: {{StartAt}} – {{EndAt}} — Lieu: {{Location}} — Voir: {{MeetingUrl}}"),
            "es" => ("Su reunión fue cancelada: {{MeetingTitle}}", "<p>Se ha cancelado una reunión que usted organiza.</p><p><strong>Reunión:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Estaba programada: {{StartAt}} – {{EndAt}}</p><p>Lugar: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Ver la reunión</a></p>", "Se ha cancelado una reunión que usted organiza. Reunión: {{MeetingTitle}} ({{MeetingType}}) — Estaba programada: {{StartAt}} – {{EndAt}} — Lugar: {{Location}} — Ver: {{MeetingUrl}}"),
            "zh" => ("您的会议已取消：{{MeetingTitle}}", "<p>您组织的一次会议已被取消。</p><p><strong>会议:</strong> {{MeetingTitle}}（{{MeetingType}}）</p><p>原定时间: {{StartAt}} – {{EndAt}}</p><p>地点: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">查看会议</a></p>", "您组织的一次会议已被取消。 会议: {{MeetingTitle}}（{{MeetingType}}） — 原定时间: {{StartAt}} – {{EndAt}} — 地点: {{Location}} — 查看: {{MeetingUrl}}"),
            "ar" => ("تم إلغاء اجتماعك: {{MeetingTitle}}", "<p>أُلغي اجتماع تنظمه.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>كان مقررًا: {{StartAt}} – {{EndAt}}</p><p>المكان: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">عرض الاجتماع</a></p>", "أُلغي اجتماع تنظمه. الاجتماع: {{MeetingTitle}} ({{MeetingType}}) — كان مقررًا: {{StartAt}} – {{EndAt}} — المكان: {{Location}} — عرض: {{MeetingUrl}}"),
            "ru" => ("Ваше совещание отменено: {{MeetingTitle}}", "<p>Совещание, которое вы организуете, отменено.</p><p><strong>Совещание:</strong> {{MeetingTitle}} ({{MeetingType}})</p><p>Было запланировано: {{StartAt}} – {{EndAt}}</p><p>Место: {{Location}}</p><p><a href=\"{{MeetingUrl}}\">Посмотреть совещание</a></p>", "Совещание, которое вы организуете, отменено. Совещание: {{MeetingTitle}} ({{MeetingType}}) — Было запланировано: {{StartAt}} – {{EndAt}} — Место: {{Location}} — Посмотреть: {{MeetingUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported meeting template locale.")
        };

        return Create("platform.meetings.organizer-cancelled", locale, subject, html, text,
            ["MeetingTitle", "MeetingType", "StartAt", "EndAt", "MeetingUrl"]);
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

    private static NotificationTemplate TimeEntryWeekSubmitted(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Timesheet to approve: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} has submitted a timesheet for your approval.</p><p><strong>Week:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Open the timesheet</a></p>", "{{PersonName}} has submitted a timesheet for your approval. Week: {{WeekLabel}} — Open it: {{TimesheetUrl}}"),
            "tr" => ("Onayınızı bekleyen zaman çizelgesi: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} onayınız için bir zaman çizelgesi gönderdi.</p><p><strong>Hafta:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgesini aç</a></p>", "{{PersonName}} onayınız için bir zaman çizelgesi gönderdi. Hafta: {{WeekLabel}} — Aç: {{TimesheetUrl}}"),
            "fr" => ("Feuille de temps à approuver : {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} a soumis une feuille de temps à votre approbation.</p><p><strong>Semaine :</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir la feuille de temps</a></p>", "{{PersonName}} a soumis une feuille de temps à votre approbation. Semaine : {{WeekLabel}} — Ouvrir : {{TimesheetUrl}}"),
            "es" => ("Parte de horas pendiente de aprobación: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} ha enviado un parte de horas para su aprobación.</p><p><strong>Semana:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Abrir el parte de horas</a></p>", "{{PersonName}} ha enviado un parte de horas para su aprobación. Semana: {{WeekLabel}} — Abrir: {{TimesheetUrl}}"),
            "zh" => ("待审批的工时表：{{PersonName}}，{{WeekLabel}}", "<p>{{PersonName}} 已提交一份工时表，等待您审批。</p><p><strong>周次：</strong>{{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">打开工时表</a></p>", "{{PersonName}} 已提交一份工时表，等待您审批。周次：{{WeekLabel}} — 打开：{{TimesheetUrl}}"),
            "ar" => ("جدول زمني بانتظار موافقتك: {{PersonName}}، {{WeekLabel}}", "<p>أرسل {{PersonName}} جدولًا زمنيًا للحصول على موافقتك.</p><p><strong>الأسبوع:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">فتح الجدول الزمني</a></p>", "أرسل {{PersonName}} جدولًا زمنيًا للحصول على موافقتك. الأسبوع: {{WeekLabel}} — فتح: {{TimesheetUrl}}"),
            "ru" => ("Табель на утверждение: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}}: табель учёта времени отправлен вам на утверждение.</p><p><strong>Неделя:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Открыть табель</a></p>", "{{PersonName}}: табель учёта времени отправлен вам на утверждение. Неделя: {{WeekLabel}} — Открыть: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.week.submitted", locale, subject, html, text, ["PersonName", "WeekLabel", "TimesheetUrl"]);
    }

    private static NotificationTemplate TimeEntryWeekWithdrawn(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Timesheet withdrawn: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} has withdrawn a submitted timesheet. There is nothing for you to approve until it is submitted again.</p><p><strong>Week:</strong> {{WeekLabel}}</p>", "{{PersonName}} has withdrawn a submitted timesheet. There is nothing for you to approve until it is submitted again. Week: {{WeekLabel}}"),
            "tr" => ("Zaman çizelgesi geri çekildi: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} gönderdiği bir zaman çizelgesini geri çekti. Yeniden gönderilene kadar onaylamanız gereken bir şey yok.</p><p><strong>Hafta:</strong> {{WeekLabel}}</p>", "{{PersonName}} gönderdiği bir zaman çizelgesini geri çekti. Yeniden gönderilene kadar onaylamanız gereken bir şey yok. Hafta: {{WeekLabel}}"),
            "fr" => ("Feuille de temps retirée : {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} a retiré une feuille de temps soumise. Vous n'avez rien à approuver tant qu'elle n'est pas soumise à nouveau.</p><p><strong>Semaine :</strong> {{WeekLabel}}</p>", "{{PersonName}} a retiré une feuille de temps soumise. Vous n'avez rien à approuver tant qu'elle n'est pas soumise à nouveau. Semaine : {{WeekLabel}}"),
            "es" => ("Parte de horas retirado: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}} ha retirado un parte de horas enviado. No tiene nada que aprobar hasta que se vuelva a enviar.</p><p><strong>Semana:</strong> {{WeekLabel}}</p>", "{{PersonName}} ha retirado un parte de horas enviado. No tiene nada que aprobar hasta que se vuelva a enviar. Semana: {{WeekLabel}}"),
            "zh" => ("工时表已撤回：{{PersonName}}，{{WeekLabel}}", "<p>{{PersonName}} 已撤回一份已提交的工时表。在其重新提交之前，您无需审批。</p><p><strong>周次：</strong>{{WeekLabel}}</p>", "{{PersonName}} 已撤回一份已提交的工时表。在其重新提交之前，您无需审批。周次：{{WeekLabel}}"),
            "ar" => ("تم سحب الجدول الزمني: {{PersonName}}، {{WeekLabel}}", "<p>سحب {{PersonName}} جدولًا زمنيًا كان قد أرسله. لا يوجد ما يلزم موافقتك عليه حتى يُعاد إرساله.</p><p><strong>الأسبوع:</strong> {{WeekLabel}}</p>", "سحب {{PersonName}} جدولًا زمنيًا كان قد أرسله. لا يوجد ما يلزم موافقتك عليه حتى يُعاد إرساله. الأسبوع: {{WeekLabel}}"),
            "ru" => ("Табель отозван: {{PersonName}}, {{WeekLabel}}", "<p>{{PersonName}}: отправленный табель отозван. Утверждать ничего не нужно, пока он не будет отправлен снова.</p><p><strong>Неделя:</strong> {{WeekLabel}}</p>", "{{PersonName}}: отправленный табель отозван. Утверждать ничего не нужно, пока он не будет отправлен снова. Неделя: {{WeekLabel}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.week.withdrawn", locale, subject, html, text, ["PersonName", "WeekLabel"]);
    }

    private static NotificationTemplate TimeEntryWeekApproved(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your timesheet was approved: {{WeekLabel}}", "<p>Your timesheet has been approved.</p><p><strong>Week:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Open your timesheet</a></p>", "Your timesheet has been approved. Week: {{WeekLabel}} — Open it: {{TimesheetUrl}}"),
            "tr" => ("Zaman çizelgeniz onaylandı: {{WeekLabel}}", "<p>Zaman çizelgeniz onaylandı.</p><p><strong>Hafta:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgenizi açın</a></p>", "Zaman çizelgeniz onaylandı. Hafta: {{WeekLabel}} — Açın: {{TimesheetUrl}}"),
            "fr" => ("Votre feuille de temps a été approuvée : {{WeekLabel}}", "<p>Votre feuille de temps a été approuvée.</p><p><strong>Semaine :</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir votre feuille de temps</a></p>", "Votre feuille de temps a été approuvée. Semaine : {{WeekLabel}} — Ouvrir : {{TimesheetUrl}}"),
            "es" => ("Su parte de horas ha sido aprobado: {{WeekLabel}}", "<p>Su parte de horas ha sido aprobado.</p><p><strong>Semana:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Abrir su parte de horas</a></p>", "Su parte de horas ha sido aprobado. Semana: {{WeekLabel}} — Abrir: {{TimesheetUrl}}"),
            "zh" => ("您的工时表已获批准：{{WeekLabel}}", "<p>您的工时表已获批准。</p><p><strong>周次：</strong>{{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">打开您的工时表</a></p>", "您的工时表已获批准。周次：{{WeekLabel}} — 打开：{{TimesheetUrl}}"),
            "ar" => ("تمت الموافقة على جدولك الزمني: {{WeekLabel}}", "<p>تمت الموافقة على جدولك الزمني.</p><p><strong>الأسبوع:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">فتح جدولك الزمني</a></p>", "تمت الموافقة على جدولك الزمني. الأسبوع: {{WeekLabel}} — فتح: {{TimesheetUrl}}"),
            "ru" => ("Ваш табель утверждён: {{WeekLabel}}", "<p>Ваш табель учёта времени утверждён.</p><p><strong>Неделя:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Открыть ваш табель</a></p>", "Ваш табель учёта времени утверждён. Неделя: {{WeekLabel}} — Открыть: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.week.approved", locale, subject, html, text, ["WeekLabel", "TimesheetUrl"]);
    }

    private static NotificationTemplate TimeEntryWeekRejected(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your timesheet was returned: {{WeekLabel}}", "<p>Your timesheet has been returned to you for changes.</p><p><strong>Week:</strong> {{WeekLabel}}</p><p><strong>Reason:</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">Open your timesheet</a></p>", "Your timesheet has been returned to you for changes. Week: {{WeekLabel}} — Reason: {{Reason}} — Open it: {{TimesheetUrl}}"),
            "tr" => ("Zaman çizelgeniz iade edildi: {{WeekLabel}}", "<p>Zaman çizelgeniz düzeltmeniz için size iade edildi.</p><p><strong>Hafta:</strong> {{WeekLabel}}</p><p><strong>Gerekçe:</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgenizi açın</a></p>", "Zaman çizelgeniz düzeltmeniz için size iade edildi. Hafta: {{WeekLabel}} — Gerekçe: {{Reason}} — Açın: {{TimesheetUrl}}"),
            "fr" => ("Votre feuille de temps vous a été renvoyée : {{WeekLabel}}", "<p>Votre feuille de temps vous a été renvoyée pour modification.</p><p><strong>Semaine :</strong> {{WeekLabel}}</p><p><strong>Motif :</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir votre feuille de temps</a></p>", "Votre feuille de temps vous a été renvoyée pour modification. Semaine : {{WeekLabel}} — Motif : {{Reason}} — Ouvrir : {{TimesheetUrl}}"),
            "es" => ("Su parte de horas ha sido devuelto: {{WeekLabel}}", "<p>Su parte de horas le ha sido devuelto para que lo modifique.</p><p><strong>Semana:</strong> {{WeekLabel}}</p><p><strong>Motivo:</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">Abrir su parte de horas</a></p>", "Su parte de horas le ha sido devuelto para que lo modifique. Semana: {{WeekLabel}} — Motivo: {{Reason}} — Abrir: {{TimesheetUrl}}"),
            "zh" => ("您的工时表已退回：{{WeekLabel}}", "<p>您的工时表已退回给您修改。</p><p><strong>周次：</strong>{{WeekLabel}}</p><p><strong>原因：</strong>{{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">打开您的工时表</a></p>", "您的工时表已退回给您修改。周次：{{WeekLabel}} — 原因：{{Reason}} — 打开：{{TimesheetUrl}}"),
            "ar" => ("أُعيد إليك جدولك الزمني: {{WeekLabel}}", "<p>أُعيد إليك جدولك الزمني لإجراء تعديلات.</p><p><strong>الأسبوع:</strong> {{WeekLabel}}</p><p><strong>السبب:</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">فتح جدولك الزمني</a></p>", "أُعيد إليك جدولك الزمني لإجراء تعديلات. الأسبوع: {{WeekLabel}} — السبب: {{Reason}} — فتح: {{TimesheetUrl}}"),
            "ru" => ("Ваш табель возвращён: {{WeekLabel}}", "<p>Ваш табель учёта времени возвращён вам на доработку.</p><p><strong>Неделя:</strong> {{WeekLabel}}</p><p><strong>Причина:</strong> {{Reason}}</p><p><a href=\"{{TimesheetUrl}}\">Открыть ваш табель</a></p>", "Ваш табель учёта времени возвращён вам на доработку. Неделя: {{WeekLabel}} — Причина: {{Reason}} — Открыть: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.week.rejected", locale, subject, html, text, ["WeekLabel", "Reason", "TimesheetUrl"]);
    }

    private static NotificationTemplate TimeEntryWeekReminder(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Reminder: your timesheet for {{WeekLabel}} is not submitted yet", "<p>Your timesheet for last week has not been submitted yet. Please complete and submit it.</p><p><strong>Week:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Open your timesheet</a></p>", "Your timesheet for last week has not been submitted yet. Please complete and submit it. Week: {{WeekLabel}} — Open it: {{TimesheetUrl}}"),
            "tr" => ("Hatırlatma: {{WeekLabel}} haftasının zaman çizelgesi henüz gönderilmedi", "<p>Geçen haftaya ait zaman çizelgeniz henüz gönderilmedi. Lütfen tamamlayıp gönderin.</p><p><strong>Hafta:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgenizi açın</a></p>", "Geçen haftaya ait zaman çizelgeniz henüz gönderilmedi. Lütfen tamamlayıp gönderin. Hafta: {{WeekLabel}} — Açın: {{TimesheetUrl}}"),
            "fr" => ("Rappel : votre feuille de temps de la semaine {{WeekLabel}} n'est pas encore soumise", "<p>Votre feuille de temps de la semaine dernière n'a pas encore été soumise. Merci de la compléter et de la soumettre.</p><p><strong>Semaine :</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir votre feuille de temps</a></p>", "Votre feuille de temps de la semaine dernière n'a pas encore été soumise. Merci de la compléter et de la soumettre. Semaine : {{WeekLabel}} — Ouvrir : {{TimesheetUrl}}"),
            "es" => ("Recordatorio: su parte de horas de la semana {{WeekLabel}} aún no se ha enviado", "<p>Su parte de horas de la semana pasada aún no se ha enviado. Por favor, complételo y envíelo.</p><p><strong>Semana:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Abrir su parte de horas</a></p>", "Su parte de horas de la semana pasada aún no se ha enviado. Por favor, complételo y envíelo. Semana: {{WeekLabel}} — Abrir: {{TimesheetUrl}}"),
            "zh" => ("提醒：您 {{WeekLabel}} 的工时表尚未提交", "<p>您上周的工时表尚未提交。请填写完整并提交。</p><p><strong>周次：</strong>{{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">打开您的工时表</a></p>", "您上周的工时表尚未提交。请填写完整并提交。周次：{{WeekLabel}} — 打开：{{TimesheetUrl}}"),
            "ar" => ("تذكير: لم يُرسَل جدولك الزمني للأسبوع {{WeekLabel}} بعد", "<p>لم يُرسَل جدولك الزمني للأسبوع الماضي بعد. يُرجى إكماله وإرساله.</p><p><strong>الأسبوع:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">فتح جدولك الزمني</a></p>", "لم يُرسَل جدولك الزمني للأسبوع الماضي بعد. يُرجى إكماله وإرساله. الأسبوع: {{WeekLabel}} — فتح: {{TimesheetUrl}}"),
            "ru" => ("Напоминание: табель за неделю {{WeekLabel}} ещё не отправлен", "<p>Ваш табель учёта времени за прошлую неделю ещё не отправлен. Пожалуйста, заполните и отправьте его.</p><p><strong>Неделя:</strong> {{WeekLabel}}</p><p><a href=\"{{TimesheetUrl}}\">Открыть ваш табель</a></p>", "Ваш табель учёта времени за прошлую неделю ещё не отправлен. Пожалуйста, заполните и отправьте его. Неделя: {{WeekLabel}} — Открыть: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.week.reminder", locale, subject, html, text, ["WeekLabel", "TimesheetUrl"]);
    }

    private static NotificationTemplate TimeEntryTimerAutoClosed(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Your timer was stopped at midnight ({{LocalDate}})", "<p>Your timer was still running at midnight, so it was stopped automatically.</p><p><strong>Day:</strong> {{LocalDate}}</p><p><strong>Recorded:</strong> {{DurationMinutes}} minutes</p><p>Please check the time on your timesheet and correct it if needed.</p><p><a href=\"{{TimesheetUrl}}\">Open your timesheet</a></p>", "Your timer was still running at midnight, so it was stopped automatically. Day: {{LocalDate}} — Recorded: {{DurationMinutes}} minutes. Please check the time on your timesheet and correct it if needed: {{TimesheetUrl}}"),
            "tr" => ("Sayacınız gece yarısı durduruldu ({{LocalDate}})", "<p>Sayacınız gece yarısı hâlâ çalışıyordu, bu yüzden otomatik olarak durduruldu.</p><p><strong>Gün:</strong> {{LocalDate}}</p><p><strong>Kaydedilen:</strong> {{DurationMinutes}} dakika</p><p>Lütfen zaman çizelgenizdeki süreyi kontrol edin, gerekirse düzeltin.</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgenizi açın</a></p>", "Sayacınız gece yarısı hâlâ çalışıyordu, bu yüzden otomatik olarak durduruldu. Gün: {{LocalDate}} — Kaydedilen: {{DurationMinutes}} dakika. Lütfen zaman çizelgenizdeki süreyi kontrol edin, gerekirse düzeltin: {{TimesheetUrl}}"),
            "fr" => ("Votre minuteur a été arrêté à minuit ({{LocalDate}})", "<p>Votre minuteur tournait encore à minuit ; il a donc été arrêté automatiquement.</p><p><strong>Jour :</strong> {{LocalDate}}</p><p><strong>Enregistré :</strong> {{DurationMinutes}} minutes</p><p>Veuillez vérifier la durée sur votre feuille de temps et la corriger si nécessaire.</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir votre feuille de temps</a></p>", "Votre minuteur tournait encore à minuit ; il a donc été arrêté automatiquement. Jour : {{LocalDate}} — Enregistré : {{DurationMinutes}} minutes. Veuillez vérifier la durée sur votre feuille de temps et la corriger si nécessaire : {{TimesheetUrl}}"),
            "es" => ("Su temporizador se detuvo a medianoche ({{LocalDate}})", "<p>Su temporizador seguía en marcha a medianoche, por lo que se detuvo automáticamente.</p><p><strong>Día:</strong> {{LocalDate}}</p><p><strong>Registrado:</strong> {{DurationMinutes}} minutos</p><p>Revise el tiempo en su parte de horas y corríjalo si es necesario.</p><p><a href=\"{{TimesheetUrl}}\">Abrir su parte de horas</a></p>", "Su temporizador seguía en marcha a medianoche, por lo que se detuvo automáticamente. Día: {{LocalDate}} — Registrado: {{DurationMinutes}} minutos. Revise el tiempo en su parte de horas y corríjalo si es necesario: {{TimesheetUrl}}"),
            "zh" => ("您的计时器已在午夜停止（{{LocalDate}}）", "<p>您的计时器在午夜时仍在运行，因此已自动停止。</p><p><strong>日期：</strong>{{LocalDate}}</p><p><strong>已记录：</strong>{{DurationMinutes}} 分钟</p><p>请检查工时表中的时间，如有需要请更正。</p><p><a href=\"{{TimesheetUrl}}\">打开您的工时表</a></p>", "您的计时器在午夜时仍在运行，因此已自动停止。日期：{{LocalDate}} — 已记录：{{DurationMinutes}} 分钟。请检查工时表中的时间，如有需要请更正：{{TimesheetUrl}}"),
            "ar" => ("تم إيقاف مؤقّتك عند منتصف الليل ({{LocalDate}})", "<p>كان مؤقّتك لا يزال يعمل عند منتصف الليل، لذلك أُوقف تلقائيًا.</p><p><strong>اليوم:</strong> {{LocalDate}}</p><p><strong>المسجَّل:</strong> {{DurationMinutes}} دقيقة</p><p>يُرجى التحقق من الوقت في جدولك الزمني وتصحيحه عند الحاجة.</p><p><a href=\"{{TimesheetUrl}}\">فتح جدولك الزمني</a></p>", "كان مؤقّتك لا يزال يعمل عند منتصف الليل، لذلك أُوقف تلقائيًا. اليوم: {{LocalDate}} — المسجَّل: {{DurationMinutes}} دقيقة. يُرجى التحقق من الوقت في جدولك الزمني وتصحيحه عند الحاجة: {{TimesheetUrl}}"),
            "ru" => ("Ваш таймер остановлен в полночь ({{LocalDate}})", "<p>В полночь ваш таймер всё ещё работал, поэтому он был остановлен автоматически.</p><p><strong>День:</strong> {{LocalDate}}</p><p><strong>Записано:</strong> {{DurationMinutes}} мин.</p><p>Проверьте время в табеле и при необходимости исправьте его.</p><p><a href=\"{{TimesheetUrl}}\">Открыть ваш табель</a></p>", "В полночь ваш таймер всё ещё работал, поэтому он был остановлен автоматически. День: {{LocalDate}} — Записано: {{DurationMinutes}} мин. Проверьте время в табеле и при необходимости исправьте его: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.timer.autoclosed", locale, subject, html, text, ["LocalDate", "DurationMinutes", "TimesheetUrl"]);
    }

    private static NotificationTemplate TimeEntryMinutesConflict(string locale)
    {
        var (subject, html, text) = locale switch
        {
            "en" => ("Please check your meeting time: {{MeetingTitle}}", "<p>The published minutes of a meeting record that you did not attend (absent or excused), but your timesheet includes time for it.</p><p><strong>Meeting:</strong> {{MeetingTitle}}</p><p><strong>Date:</strong> {{MeetingDate}}</p><p>Please check the entry and correct it if needed.</p><p><a href=\"{{TimesheetUrl}}\">Open your timesheet</a></p>", "The published minutes of a meeting record that you did not attend (absent or excused), but your timesheet includes time for it. Meeting: {{MeetingTitle}} — Date: {{MeetingDate}}. Please check the entry and correct it if needed: {{TimesheetUrl}}"),
            "tr" => ("Toplantı sürenizi kontrol edin: {{MeetingTitle}}", "<p>Bir toplantının yayımlanan tutanağında katılmadığınız (devamsız ya da mazeretli) kayıtlı; ancak zaman çizelgenizde bu toplantı için süre var.</p><p><strong>Toplantı:</strong> {{MeetingTitle}}</p><p><strong>Tarih:</strong> {{MeetingDate}}</p><p>Lütfen kaydı kontrol edin, gerekirse düzeltin.</p><p><a href=\"{{TimesheetUrl}}\">Zaman çizelgenizi açın</a></p>", "Bir toplantının yayımlanan tutanağında katılmadığınız (devamsız ya da mazeretli) kayıtlı; ancak zaman çizelgenizde bu toplantı için süre var. Toplantı: {{MeetingTitle}} — Tarih: {{MeetingDate}}. Lütfen kaydı kontrol edin, gerekirse düzeltin: {{TimesheetUrl}}"),
            "fr" => ("Veuillez vérifier le temps de votre réunion : {{MeetingTitle}}", "<p>Le procès-verbal publié d'une réunion indique votre absence (excusée ou non), mais votre feuille de temps comporte du temps pour cette réunion.</p><p><strong>Réunion :</strong> {{MeetingTitle}}</p><p><strong>Date :</strong> {{MeetingDate}}</p><p>Veuillez vérifier la saisie et la corriger si nécessaire.</p><p><a href=\"{{TimesheetUrl}}\">Ouvrir votre feuille de temps</a></p>", "Le procès-verbal publié d'une réunion indique votre absence (excusée ou non), mais votre feuille de temps comporte du temps pour cette réunion. Réunion : {{MeetingTitle}} — Date : {{MeetingDate}}. Veuillez vérifier la saisie et la corriger si nécessaire : {{TimesheetUrl}}"),
            "es" => ("Revise el tiempo de su reunión: {{MeetingTitle}}", "<p>El acta publicada de una reunión indica que no asistió (ausencia justificada o no), pero su parte de horas incluye tiempo para esa reunión.</p><p><strong>Reunión:</strong> {{MeetingTitle}}</p><p><strong>Fecha:</strong> {{MeetingDate}}</p><p>Revise la entrada y corríjala si es necesario.</p><p><a href=\"{{TimesheetUrl}}\">Abrir su parte de horas</a></p>", "El acta publicada de una reunión indica que no asistió (ausencia justificada o no), pero su parte de horas incluye tiempo para esa reunión. Reunión: {{MeetingTitle}} — Fecha: {{MeetingDate}}. Revise la entrada y corríjala si es necesario: {{TimesheetUrl}}"),
            "zh" => ("请核对您的会议时间：{{MeetingTitle}}", "<p>某次会议已发布的会议纪要记录您未出席（缺席或请假），但您的工时表中包含该会议的时间。</p><p><strong>会议：</strong>{{MeetingTitle}}</p><p><strong>日期：</strong>{{MeetingDate}}</p><p>请核对该条记录，如有需要请更正。</p><p><a href=\"{{TimesheetUrl}}\">打开您的工时表</a></p>", "某次会议已发布的会议纪要记录您未出席（缺席或请假），但您的工时表中包含该会议的时间。会议：{{MeetingTitle}} — 日期：{{MeetingDate}}。请核对该条记录，如有需要请更正：{{TimesheetUrl}}"),
            "ar" => ("يُرجى التحقق من وقت اجتماعك: {{MeetingTitle}}", "<p>يسجّل محضر الاجتماع المنشور غيابك عن الاجتماع (بعذر أو بدون عذر)، لكن جدولك الزمني يتضمن وقتًا لهذا الاجتماع.</p><p><strong>الاجتماع:</strong> {{MeetingTitle}}</p><p><strong>التاريخ:</strong> {{MeetingDate}}</p><p>يُرجى التحقق من الإدخال وتصحيحه عند الحاجة.</p><p><a href=\"{{TimesheetUrl}}\">فتح جدولك الزمني</a></p>", "يسجّل محضر الاجتماع المنشور غيابك عن الاجتماع (بعذر أو بدون عذر)، لكن جدولك الزمني يتضمن وقتًا لهذا الاجتماع. الاجتماع: {{MeetingTitle}} — التاريخ: {{MeetingDate}}. يُرجى التحقق من الإدخال وتصحيحه عند الحاجة: {{TimesheetUrl}}"),
            "ru" => ("Проверьте время совещания: {{MeetingTitle}}", "<p>В опубликованном протоколе совещания указано, что вас не было (отсутствие или уважительная причина), но в вашем табеле есть время на это совещание.</p><p><strong>Совещание:</strong> {{MeetingTitle}}</p><p><strong>Дата:</strong> {{MeetingDate}}</p><p>Проверьте запись и при необходимости исправьте её.</p><p><a href=\"{{TimesheetUrl}}\">Открыть ваш табель</a></p>", "В опубликованном протоколе совещания указано, что вас не было (отсутствие или уважительная причина), но в вашем табеле есть время на это совещание. Совещание: {{MeetingTitle}} — Дата: {{MeetingDate}}. Проверьте запись и при необходимости исправьте её: {{TimesheetUrl}}"),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Unsupported time-entry template locale.")
        };

        return Create("timeentry.meeting.minutesconflict", locale, subject, html, text, ["MeetingTitle", "MeetingDate", "TimesheetUrl"]);
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
