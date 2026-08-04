// The UI language set: Azerbaijani (default), Russian and English. Stage 1
// (functionality-spec.md §2) specified only the first two — English was added afterwards, for
// the public landing page and for tenants whose staff do not read either.
//
// Every user-visible string in the app comes from here rather than being hardcoded in a
// component. The `satisfies Record<string, Dict>` at the bottom is what makes that hold: `Dict`
// requires all three languages, so a half-translated key is a build failure, not a blank cell
// someone notices in production.

export type Language = "az" | "ru" | "en";

/** Display order of the switch, and the single list a fourth language would be added to. */
export const LANGUAGES: readonly Language[] = ["az", "ru", "en"];

export const DEFAULT_LANGUAGE: Language = "az";

/** Guards the value read back from localStorage, which is a string from outside the type system. */
export function isLanguage(value: unknown): value is Language {
  return typeof value === "string" && (LANGUAGES as readonly string[]).includes(value);
}

type Dict = Record<Language, string>;

export const translations = {
  // ---- Landing page (public) ----
  // Marketing copy, and the only place in the app that speaks to someone who is not a
  // customer yet. Module statuses are deliberately honest: "Tezliklə" means it does not
  // exist, and must stay that way until it does.
  landingMotto: {
    az: "Siz dincəlin — biznesiniz işləsin.",
    ru: "Вы отдыхаете — бизнес работает.",
    en: "You rest — your business keeps working.",
  },
  landingHeroTitle: {
    az: "Zənglərinizə süni intellekt agentləri cavab verir",
    ru: "На ваши звонки отвечают ИИ-агенты",
    en: "AI agents answer your calls",
  },
  landingHeroLead: {
    az: "Agentlər müştərilərinizin zənglərini 24/7 Azərbaycan, rus və ingilis dillərində qarşılayır, randevu yazır və nəticəni sisteminizə köçürür.",
    ru: "Агенты круглосуточно принимают звонки ваших клиентов на азербайджанском, русском и английском, записывают на приём и переносят результат в вашу систему.",
    en: "Agents take your customers' calls around the clock in Azerbaijani, Russian and English, book the appointment and write the result straight into your system.",
  },

  landingHowTitle: { az: "Necə işləyir", ru: "Как это работает", en: "How it works" },
  landingStep1Title: { az: "Müştəri zəng edir", ru: "Клиент звонит", en: "A customer calls" },
  landingStep1Text: {
    az: "Xətt heç vaxt məşğul olmur — gecə, həftəsonu və bayram günlərində də.",
    ru: "Линия никогда не занята — ночью, в выходные и в праздники тоже.",
    en: "The line is never busy — nights, weekends and holidays included.",
  },
  landingStep2Title: { az: "Agent cavab verir", ru: "Агент отвечает", en: "The agent answers" },
  landingStep2Text: {
    az: "Müştərinin dilində danışır, sualı başa düşür, boş vaxtları yoxlayır və razılaşır.",
    ru: "Говорит на языке клиента, понимает вопрос, проверяет свободное время и договаривается.",
    en: "Speaks the customer's language, understands the request, checks what is free and agrees a time.",
  },
  landingStep3Title: {
    az: "Nəticə sistemə düşür",
    ru: "Результат попадает в систему",
    en: "The result lands in your system",
  },
  landingStep3Text: {
    az: "Randevu təqvimə yazılır, zəng və danışığın mətni jurnalda saxlanılır.",
    ru: "Запись попадает в календарь, звонок и текст разговора сохраняются в журнале.",
    en: "The appointment goes into the calendar; the call and its transcript are kept in the log.",
  },

  landingModulesTitle: { az: "Modullar", ru: "Модули", en: "Modules" },
  landingModulesLead: {
    az: "Eyni səs agenti müxtəlif işlər üçün. Yeni modullar əlavə olunur.",
    ru: "Один и тот же голосовой агент для разных задач. Новые модули добавляются.",
    en: "The same voice agent, put to different jobs. New modules are being added.",
  },
  landingStatusActive: { az: "Aktiv", ru: "Активен", en: "Active" },
  landingStatusSoon: { az: "Tezliklə", ru: "Скоро", en: "Coming soon" },

  moduleAppointmentsTitle: { az: "Randevu", ru: "Запись на приём", en: "Appointments" },
  moduleAppointmentsText: {
    az: "Axşam saat 23:00-da zəng edən müştəri səhərə qədər gözləmir — başqa yerə yazılır. Agent həmin an yazır, təqvim özü dolur.",
    ru: "Клиент, позвонивший в 23:00, не станет ждать до утра — он запишется в другом месте. Агент записывает сразу, календарь заполняется сам.",
    en: "A customer who calls at 11pm will not wait until morning — they book somewhere else. The agent books them there and then, and the calendar fills itself.",
  },
  moduleInfoTitle: { az: "Məlumat xətti", ru: "Информационная линия", en: "Information line" },
  moduleInfoText: {
    az: "«Neçəyədir?», «Saat neçəyə kimi işləyirsiniz?» — gündə onlarla eyni sual. İşçinizin vaxtı bunlara getməsin.",
    ru: "«Сколько стоит?», «До скольки работаете?» — десятки одинаковых вопросов в день. Пусть время сотрудника уходит не на них.",
    en: "“How much is it?”, “How late are you open?” — dozens of identical questions a day. Your staff's time should not go on those.",
  },
  moduleRemindersTitle: {
    az: "Xatırlatma və təsdiq",
    ru: "Напоминание и подтверждение",
    en: "Reminders and confirmations",
  },
  moduleRemindersText: {
    az: "Gəlməyən müştəri boş qalmış saat və itirilmiş gəlir deməkdir. Bir gün əvvəlki zəng həmin saatı satmağa imkan verir.",
    ru: "Неявка — это пустой час и потерянная выручка. Звонок накануне даёт возможность продать этот час.",
    en: "A no-show is an empty hour and lost revenue. A call the day before gives you the chance to sell that hour to somebody else.",
  },
  moduleFeedbackTitle: {
    az: "Rəy və məmnuniyyət",
    ru: "Отзывы и удовлетворённость",
    en: "Feedback and satisfaction",
  },
  moduleFeedbackText: {
    az: "Narazı müştəri şikayət etmir — sadəcə qayıtmır. Xidmətdən sonrakı zəng problemi rəy saytlarına düşməzdən əvvəl tapır.",
    ru: "Недовольный клиент не жалуется — он просто не возвращается. Звонок после услуги находит проблему раньше, чем она попадёт в отзывы.",
    en: "An unhappy customer does not complain — they simply do not come back. A call after the service finds the problem before it reaches the review sites.",
  },
  moduleOrdersTitle: { az: "Sifariş qəbulu", ru: "Приём заказов", en: "Order taking" },
  moduleOrdersText: {
    az: "Pik saatda məşğul xətt birbaşa itirilmiş sifarişdir. Eyni anda neçə zəngin gəlməsinin fərqi yoxdur.",
    ru: "Занятая линия в час пик — это напрямую потерянный заказ. Неважно, сколько звонков приходит одновременно.",
    en: "A busy line at peak hour is a lost order, plainly. It makes no difference how many calls arrive at once.",
  },
  moduleSurveysTitle: { az: "Sorğu və araşdırma", ru: "Опросы и исследования", en: "Surveys and research" },
  moduleSurveysText: {
    az: "500 müştəriyə zəng etmək bir işçinin bir həftəsidir. Agent üçün bu, bir gecədir.",
    ru: "Обзвонить 500 клиентов — это неделя работы сотрудника. Для агента — одна ночь.",
    en: "Calling 500 customers is a week of somebody's work. For an agent it is one night.",
  },

  landingWhyTitle: { az: "Niyə sekretar.az", ru: "Почему sekretar.az", en: "Why sekretar.az" },
  landingWhy1Title: { az: "Üç dildə danışır", ru: "Говорит на трёх языках", en: "Speaks three languages" },
  landingWhy1Text: {
    az: "Azərbaycan, rus və ingilis dilləri. Müştəri söhbətə hansı dildə başlayırsa, cavab da o dildə gəlir.",
    ru: "Азербайджанский, русский и английский. На каком языке клиент начал разговор — на том и получит ответ.",
    en: "Azerbaijani, Russian and English. Whichever language the customer opens with is the one they get back.",
  },
  landingWhy2Title: { az: "24/7 açıqdır", ru: "Открыто 24/7", en: "Open 24/7" },
  landingWhy2Text: {
    az: "Cavabsız zəng itirilmiş müştəri deməkdir. Bu xətt nə nahara çıxır, nə xəstələnir, nə də məzuniyyətə gedir.",
    ru: "Пропущенный звонок — потерянный клиент. Эта линия не уходит на обед, не болеет и не берёт отпуск.",
    en: "A missed call is a lost customer. This line does not break for lunch, does not fall ill and does not take holiday.",
  },
  landingWhy3Title: {
    az: "Hər danışığın mətni",
    ru: "Текст каждого разговора",
    en: "A transcript of every conversation",
  },
  landingWhy3Text: {
    az: "Hər zəng tam mətnə çevrilir. Nəyin necə deyildiyini oxuyur, keyfiyyəti özünüz yoxlayırsınız — səsə qulaq asmadan.",
    ru: "Каждый звонок превращается в текст. Вы читаете, что и как было сказано, и проверяете качество сами — не прослушивая запись.",
    en: "Every call is turned into text. You read what was said and how it was said, and check quality yourself — without listening to a recording.",
  },
  landingWhy4Title: { az: "Hər zəng ölçülür", ru: "Каждый звонок измерим", en: "Every call is measured" },
  landingWhy4Text: {
    az: "Müddət, nəticə və dəqiq maya dəyəri jurnalda saxlanılır. Nəyə nə qədər xərclədiyinizi təxmin etmək lazım deyil.",
    ru: "Длительность, результат и точная себестоимость сохраняются в журнале. Гадать, во сколько вам это обошлось, не нужно.",
    en: "Duration, outcome and exact cost are kept in the log. No guessing what any of it came to.",
  },
  landingWhy5Title: { az: "İnsana ötürmə", ru: "Передача человеку", en: "Hand-off to a person" },
  landingWhy5Text: {
    az: "Agent həll edə bilmirsə, zəng dayanmır — dərhal işçiyə yönləndirilir.",
    ru: "Если агент не справляется, звонок не обрывается — он сразу переводится на сотрудника.",
    en: "If the agent cannot handle it, the call does not end — it goes straight through to a member of staff.",
  },
  landingWhy6Title: { az: "Sistemə özü yazılır", ru: "Само попадает в систему", en: "Writes itself into your system" },
  landingWhy6Text: {
    az: "Randevu təqvimə, müştəri bazaya, zəng jurnala düşür. Sonradan əl ilə köçürmək lazım deyil.",
    ru: "Запись — в календарь, клиент — в базу, звонок — в журнал. Ничего не нужно потом переносить вручную.",
    en: "Appointment to the calendar, customer to the database, call to the log. Nothing to copy over afterwards.",
  },

  landingFooterTagline: {
    az: "Azərbaycan, rus və ingilis dillərində danışan süni intellekt agentləri.",
    ru: "ИИ-агенты, говорящие на азербайджанском, русском и английском.",
    en: "AI agents that speak Azerbaijani, Russian and English.",
  },
  landingFooterRights: { az: "Bütün hüquqlar qorunur.", ru: "Все права защищены.", en: "All rights reserved." },

  // ---- Login ----
  signIn: { az: "Daxil ol", ru: "Войти", en: "Sign in" },
  email: { az: "E-poçt", ru: "Эл. почта", en: "Email" },
  password: { az: "Şifrə", ru: "Пароль", en: "Password" },
  invalidCredentials: {
    az: "E-poçt və ya şifrə yanlışdır.",
    ru: "Неверный email или пароль.",
    en: "Email or password is incorrect.",
  },
  somethingWentWrong: {
    az: "Xəta baş verdi. Yenidən cəhd edin.",
    ru: "Что-то пошло не так. Попробуйте снова.",
    en: "Something went wrong. Please try again.",
  },
  emailAndPasswordRequired: {
    az: "E-poçt və şifrə tələb olunur.",
    ru: "Email и пароль обязательны.",
    en: "Email and password are required.",
  },

  // ---- Shared controls ----
  close: { az: "Bağla", ru: "Закрыть", en: "Close" },

  // ---- Nav ----
  navCalendar: { az: "Təqvim", ru: "Календарь", en: "Calendar" },
  navServices: { az: "Xidmətlər", ru: "Услуги", en: "Services" },
  navProviders: { az: "İcraçılar", ru: "Специалисты", en: "Providers" },
  navClients: { az: "Müştərilər", ru: "Клиенты", en: "Clients" },
  navAdmin: { az: "İdarəetmə", ru: "Администрирование", en: "Administration" },
  navCallLog: { az: "Zəng jurnalı", ru: "Журнал звонков", en: "Call log" },
  navCall: { az: "Zəng et", ru: "Позвонить", en: "Make a call" },
  navDashboard: { az: "Göstəricilər", ru: "Показатели", en: "Dashboard" },
  navTenants: { az: "Müştərilər", ru: "Клиенты", en: "Clients" },

  // ---- Modules ----
  /** Only ever shown to a tenant holding more than one, which by design is the rare case. */
  navSwitchModule: { az: "Bölmələr", ru: "Разделы", en: "Modules" },
  pickerTitle: {
    az: "Hansı bölmə ilə işləyəcəksiniz?",
    ru: "С каким разделом работать?",
    en: "Which module do you want?",
  },
  noModulesTitle: { az: "Aktiv bölmə yoxdur", ru: "Нет активных разделов", en: "No modules enabled" },
  noModulesBody: {
    az: "Hesabınıza hələ heç bir bölmə təyin edilməyib. Zəhmət olmasa bizimlə əlaqə saxlayın.",
    ru: "Вашей учётной записи пока не назначен ни один раздел. Пожалуйста, свяжитесь с нами.",
    en: "No module has been assigned to your account yet. Please get in touch with us.",
  },
  /** Platform-admin tenant detail. */
  tenantModules: { az: "Bölmələr", ru: "Разделы", en: "Modules" },
  tenantModulesHint: {
    az: "Müştərinin hansı bölmələrə girişi olduğunu buradan idarə edin.",
    ru: "Здесь настраивается, к каким разделам у клиента есть доступ.",
    en: "Controls which modules this client can use.",
  },
  moduleNotBuiltYet: { az: "Hazırlanır", ru: "В разработке", en: "Not built yet" },
  signOut: { az: "Çıxış", ru: "Выйти", en: "Sign out" },

  // ---- Live call page ----
  agentRole: { az: "Rəqəmsal köməkçi", ru: "Цифровой ассистент", en: "Digital assistant" },
  callPipeline: { az: "Rejim", ru: "Режим", en: "Mode" },
  callPipelineLegend: { az: "Rejimlər — izah", ru: "Режимы — пояснение", en: "Modes — explained" },
  callPipelineWhat: { az: "Nədir", ru: "Что это", en: "What it is" },
  callPipelineTradeOff: { az: "Güzəşt", ru: "Компромисс", en: "Trade-off" },
  callPipelineNote: {
    az: "Zəng zamanı rejimi dəyişmək olmaz — səs tezliyi zəngin əvvəlində təyin olunur. Hər zəngin rejimi jurnalda saxlanılır.",
    ru: "Во время звонка режим менять нельзя — частота дискретизации задаётся в начале. Режим каждого звонка сохраняется в журнале.",
    en: "The mode cannot be changed mid-call — the sample rate is fixed when the call starts. Each call's mode is kept in the log.",
  },
  callHint: {
    az: "Telefon düyməsini basın — Lamiya cavab verəcək. Mikrofonunuza icazə tələb olunacaq.",
    ru: "Нажмите кнопку телефона — Лямия ответит. Понадобится доступ к микрофону.",
    en: "Press the phone button and Lamiya will answer. You will be asked for microphone access.",
  },
  callPressToDial: {
    az: "Zəng etmək üçün düyməni basın",
    ru: "Нажмите кнопку, чтобы позвонить",
    en: "Press the button to call",
  },
  callConnecting: { az: "Qoşulur…", ru: "Соединение…", en: "Connecting…" },
  callInProgress: { az: "Zəng davam edir", ru: "Идёт разговор", en: "Call in progress" },
  callEnded: { az: "Zəng bitdi", ru: "Звонок завершён", en: "Call ended" },
  callMicDenied: {
    az: "Mikrofona icazə verilmədi. Brauzer parametrlərindən icazə verin.",
    ru: "Нет доступа к микрофону. Разрешите доступ в настройках браузера.",
    en: "Microphone access was denied. Allow it in your browser settings.",
  },
  callFailed: {
    az: "Zəng alınmadı. Yenidən cəhd edin.",
    ru: "Звонок не удался. Попробуйте снова.",
    en: "The call did not go through. Please try again.",
  },
  callDial: { az: "Zəng et", ru: "Позвонить", en: "Call" },
  callHangUp: { az: "Zəngi bitir", ru: "Завершить звонок", en: "End call" },

  roleOwner: { az: "Sahibkar", ru: "Владелец", en: "Owner" },
  roleStaff: { az: "İşçi", ru: "Сотрудник", en: "Staff" },
  rolePlatformAdmin: { az: "Platforma admini", ru: "Администратор платформы", en: "Platform admin" },
  roleAgent: { az: "Süni intellekt", ru: "ИИ-агент", en: "AI agent" },

  brandPlatform: { az: "sekretar.az — Platforma", ru: "sekretar.az — Платформа", en: "sekretar.az — Platform" },
  brandGeneric: { az: "Görüş Platforması", ru: "Платформа записи", en: "Appointment platform" },
  signedInAsProductOwner: {
    az: "Məhsul sahibi kimi daxil olub",
    ru: "Вы вошли как владелец продукта",
    en: "Signed in as product owner",
  },

  // ---- Tenant List ----
  tenants: { az: "Müştərilər", ru: "Клиенты", en: "Clients" },
  searchTenants: { az: "Müştəri axtar…", ru: "Поиск клиентов…", en: "Search clients…" },
  createTenant: { az: "+ Müştəri yarat", ru: "+ Создать клиента", en: "+ Create client" },
  colName: { az: "Ad", ru: "Название", en: "Name" },
  colTimezone: { az: "Saat qurşağı", ru: "Часовой пояс", en: "Time zone" },
  colPhoneLine: { az: "Telefon xətti", ru: "Телефонная линия", en: "Phone line" },
  colStatus: { az: "Status", ru: "Статус", en: "Status" },
  colCreated: { az: "Yaradılıb", ru: "Создано", en: "Created" },
  unassigned: { az: "(təyin edilməyib)", ru: "(не назначено)", en: "(unassigned)" },
  statusActive: { az: "Aktiv", ru: "Активен", en: "Active" },
  statusInactive: { az: "Deaktiv", ru: "Неактивен", en: "Inactive" },
  noTenantsYet: {
    az: "Hələ müştəri yoxdur. Başlamaq üçün ilkini yaradın.",
    ru: "Пока нет клиентов. Создайте первого, чтобы начать.",
    en: "No clients yet. Create the first one to get started.",
  },
  rowClickToTenantDetail: {
    az: "Sətrə klikləyin → Müştəri detalları.",
    ru: "Нажмите на строку → Детали клиента.",
    en: "Click a row → client details.",
  },
  failedToLoadTenants: {
    az: "Müştərilər yüklənmədi.",
    ru: "Не удалось загрузить клиентов.",
    en: "Couldn't load clients.",
  },

  // ---- Create Tenant ----
  breadcrumbTenantsCreate: {
    az: "Müştərilər / Müştəri yarat",
    ru: "Клиенты / Создать клиента",
    en: "Clients / Create client",
  },
  businessName: { az: "Biznes adı", ru: "Название бизнеса", en: "Business name" },
  businessNamePlaceholder: {
    az: "məs. Bakı Bərbər Salonu",
    ru: "напр. Баку Барбершоп",
    en: "e.g. Baku Barbershop",
  },
  timezone: { az: "Saat qurşağı", ru: "Часовой пояс", en: "Time zone" },
  phoneLine: { az: "Telefon xətti", ru: "Телефонная линия", en: "Phone line" },
  phoneLinePlaceholder: {
    az: "Təyin edilməyib — sonra qoşulacaq",
    ru: "Не назначено — подключим позже",
    en: "Unassigned — to be connected later",
  },
  firstOwnerAccount: { az: "İlk sahibkar hesabı", ru: "Первый аккаунт владельца", en: "First owner account" },
  ownerName: { az: "Sahibkarın adı", ru: "Имя владельца", en: "Owner's name" },
  fullName: { az: "Tam ad", ru: "Полное имя", en: "Full name" },
  ownerEmail: { az: "Sahibkarın e-poçtu", ru: "Email владельца", en: "Owner's email" },
  ownerPassword: { az: "Sahibkarın şifrəsi", ru: "Пароль владельца", en: "Owner's password" },
  cancel: { az: "Ləğv et", ru: "Отмена", en: "Cancel" },
  businessNameRequired: {
    az: "Biznes adı tələb olunur.",
    ru: "Название бизнеса обязательно.",
    en: "Business name is required.",
  },
  ownerNameRequired: {
    az: "Sahibkarın adı tələb olunur.",
    ru: "Имя владельца обязательно.",
    en: "Owner's name is required.",
  },
  enterValidEmail: { az: "Düzgün e-poçt daxil edin.", ru: "Введите корректный email.", en: "Enter a valid email." },
  atLeast8Characters: { az: "Ən azı 8 simvol.", ru: "Минимум 8 символов.", en: "At least 8 characters." },

  // ---- Tenant Detail ----
  saveChanges: { az: "Dəyişiklikləri saxla", ru: "Сохранить изменения", en: "Save changes" },
  deactivateTenant: { az: "Müştərini deaktiv et", ru: "Деактивировать клиента", en: "Deactivate client" },
  reactivateTenant: { az: "Müştərini aktivləşdir", ru: "Реактивировать клиента", en: "Reactivate client" },
  backToList: { az: "Siyahıya qayıt", ru: "Назад к списку", en: "Back to list" },
  ownerAccounts: { az: "Sahibkar hesabları", ru: "Аккаунты владельца", en: "Owner accounts" },
  noOwnerAccountsYet: {
    az: "Hələ sahibkar hesabı yoxdur.",
    ru: "Пока нет аккаунтов владельца.",
    en: "No owner accounts yet.",
  },
  failedToLoadOwnerAccounts: {
    az: "Sahibkar hesabları yüklənmədi.",
    ru: "Не удалось загрузить аккаунты владельца.",
    en: "Couldn't load owner accounts.",
  },

  // ---- Calendar ----
  newAppointment: { az: "+ Yeni görüş", ru: "+ Новая запись", en: "+ New appointment" },
  viewDay: { az: "Gün", ru: "День", en: "Day" },
  viewWeek: { az: "Həftə", ru: "Неделя", en: "Week" },
  viewMonth: { az: "Ay", ru: "Месяц", en: "Month" },
  today: { az: "Bu gün", ru: "Сегодня", en: "Today" },
  allProviders: { az: "Bütün icraçılar", ru: "Все специалисты", en: "All providers" },
  failedToLoadAppointments: {
    az: "Görüşlər yüklənmədi.",
    ru: "Не удалось загрузить записи.",
    en: "Couldn't load appointments.",
  },
  noAnswerReminder: { az: "cavab yoxdur (xatırlatma)", ru: "нет ответа (напоминание)", en: "no answer (reminder)" },

  // ---- Appointment Panel ----
  editAppointment: { az: "Görüşü redaktə et", ru: "Изменить запись", en: "Edit appointment" },
  clientPhone: { az: "Müştərinin telefonu", ru: "Телефон клиента", en: "Client's phone" },
  clientName: { az: "Müştərinin adı", ru: "Имя клиента", en: "Client's name" },
  service: { az: "Xidmət", ru: "Услуга", en: "Service" },
  provider: { az: "İcraçı", ru: "Специалист", en: "Provider" },
  dateAndTime: { az: "Tarix və vaxt", ru: "Дата и время", en: "Date and time" },
  notes: { az: "Qeydlər", ru: "Заметки", en: "Notes" },
  optional: { az: "İstəyə bağlı", ru: "Необязательно", en: "Optional" },
  save: { az: "Yadda saxla", ru: "Сохранить", en: "Save" },
  cancelAppointment: { az: "Görüşü ləğv et", ru: "Отменить запись", en: "Cancel appointment" },
  confirmCancel: { az: "Ləğvi təsdiqləyin?", ru: "Подтвердить отмену?", en: "Confirm cancellation?" },
  cancelConfirmationNote: {
    az: "Ləğv etmək tətbiq olunmadan əvvəl təsdiq istəyir.",
    ru: "Отмена требует подтверждения перед применением.",
    en: "Cancelling asks for confirmation before it is applied.",
  },
  clientPhoneRequired: {
    az: "Müştərinin telefonu tələb olunur.",
    ru: "Телефон клиента обязателен.",
    en: "Client's phone is required.",
  },
  chooseAProvider: { az: "İcraçı seçin.", ru: "Выберите специалиста.", en: "Choose a provider." },
  chooseAService: { az: "Xidmət seçin.", ru: "Выберите услугу.", en: "Choose a service." },
  chooseDateAndTime: { az: "Tarix və vaxt seçin.", ru: "Выберите дату и время.", en: "Choose a date and time." },

  // ---- Services ----
  services: { az: "Xidmətlər", ru: "Услуги", en: "Services" },
  ownerViewEditable: {
    az: "Sahibkar görünüşü — redaktə edilə bilər",
    ru: "Вид владельца — редактируется",
    en: "Owner view — editable",
  },
  staffViewReadOnly: {
    az: "İşçi görünüşü — yalnız oxumaq üçün",
    ru: "Вид сотрудника — только для чтения",
    en: "Staff view — read-only",
  },
  addService: { az: "+ Xidmət əlavə et", ru: "+ Добавить услугу", en: "+ Add service" },
  colPrice: { az: "Qiymət", ru: "Цена", en: "Price" },
  colDuration: { az: "Təxmini müddət", ru: "Примерная длительность", en: "Approx. duration" },
  edit: { az: "Redaktə et", ru: "Изменить", en: "Edit" },
  remove: { az: "Sil", ru: "Удалить", en: "Delete" },
  noServicesYet: { az: "Hələ xidmət yoxdur.", ru: "Пока нет услуг.", en: "No services yet." },
  failedToLoadServices: {
    az: "Xidmətlər yüklənmədi.",
    ru: "Не удалось загрузить услуги.",
    en: "Couldn't load services.",
  },
  editService: { az: "Xidməti redaktə et", ru: "Изменить услугу", en: "Edit service" },
  name: { az: "Ad", ru: "Название", en: "Name" },
  priceAzn: { az: "Qiymət (AZN)", ru: "Цена (AZN)", en: "Price (AZN)" },
  durationMin: { az: "Müddət (dəq)", ru: "Длительность (мин)", en: "Duration (min)" },
  enterValidPrice: { az: "Düzgün qiymət daxil edin.", ru: "Введите корректную цену.", en: "Enter a valid price." },
  enterValidDuration: {
    az: "Düzgün müddət daxil edin.",
    ru: "Введите корректную длительность.",
    en: "Enter a valid duration.",
  },
  serviceNameRequired: {
    az: "Xidmətin adı tələb olunur.",
    ru: "Название услуги обязательно.",
    en: "Service name is required.",
  },

  // ---- Providers ----
  providers: { az: "İcraçılar", ru: "Специалисты", en: "Providers" },
  addProvider: { az: "+ İcraçı əlavə et", ru: "+ Добавить специалиста", en: "+ Add provider" },
  noProvidersYet: { az: "Hələ icraçı yoxdur.", ru: "Пока нет специалистов.", en: "No providers yet." },
  serviceMatrix: { az: "İcraçı–xidmət uyğunluğu", ru: "Услуги специалистов", en: "Provider–service matrix" },
  serviceMatrixHint: {
    az: "İşarələnmiş xidmətləri icraçı yerinə yetirir. Süni intellekt yalnız uyğun icraçıları təklif edir.",
    ru: "Отмеченные услуги выполняет специалист. ИИ-агент предлагает только подходящих специалистов.",
    en: "A ticked service is one the provider performs. The AI agent only offers providers who can do the job.",
  },
  failedToLoadProviders: {
    az: "İcraçılar yüklənmədi.",
    ru: "Не удалось загрузить специалистов.",
    en: "Couldn't load providers.",
  },
  editProvider: { az: "İcraçını redaktə et", ru: "Изменить специалиста", en: "Edit provider" },

  // ---- Admin (tenant self-service) ----
  adminTitle: { az: "İdarəetmə", ru: "Администрирование", en: "Administration" },
  businessDetails: { az: "Biznes məlumatları", ru: "Данные бизнеса", en: "Business details" },
  staffAccounts: { az: "Hesablar", ru: "Аккаунты", en: "Accounts" },
  addStaff: { az: "+ İşçi əlavə et", ru: "+ Добавить сотрудника", en: "+ Add staff member" },
  colEmail: { az: "E-poçt", ru: "Email", en: "Email" },
  colRole: { az: "Rol", ru: "Роль", en: "Role" },
  noStaffAccountsYet: {
    az: "Hələ işçi hesabı yoxdur.",
    ru: "Пока нет аккаунтов сотрудников.",
    en: "No staff accounts yet.",
  },
  editAccount: { az: "Hesabı redaktə et", ru: "Изменить аккаунт", en: "Edit account" },
  resetPassword: { az: "Şifrəni yenilə", ru: "Сбросить пароль", en: "Reset password" },
  newPassword: { az: "Yeni şifrə", ru: "Новый пароль", en: "New password" },
  failedToLoadAccounts: {
    az: "Hesablar yüklənmədi.",
    ru: "Не удалось загрузить аккаунты.",
    en: "Couldn't load accounts.",
  },
  failedToLoadTenant: {
    az: "Biznes məlumatları yüklənmədi.",
    ru: "Не удалось загрузить данные бизнеса.",
    en: "Couldn't load business details.",
  },
  add: { az: "Əlavə et", ru: "Добавить", en: "Add" },
  nameRequired: { az: "Ad tələb olunur.", ru: "Имя обязательно.", en: "Name is required." },

  // ---- Clients ----
  clientsTitle: { az: "Müştərilər", ru: "Клиенты", en: "Clients" },
  addClient: { az: "+ Müştəri əlavə et", ru: "+ Добавить клиента", en: "+ Add client" },
  editClient: { az: "Müştərini redaktə et", ru: "Изменить клиента", en: "Edit client" },
  colPhone: { az: "Telefon", ru: "Телефон", en: "Phone" },
  noClientsYet: { az: "Hələ müştəri yoxdur.", ru: "Пока нет клиентов.", en: "No clients yet." },
  failedToLoadClients: {
    az: "Müştərilər yüklənmədi.",
    ru: "Не удалось загрузить клиентов.",
    en: "Couldn't load clients.",
  },
  phoneNumberRequired: {
    az: "Telefon nömrəsi tələb olunur.",
    ru: "Номер телефона обязателен.",
    en: "Phone number is required.",
  },
  duplicatePhoneNumber: {
    az: "Bu nömrə ilə müştəri artıq mövcuddur.",
    ru: "Клиент с таким номером уже существует.",
    en: "A client with this number already exists.",
  },
  blackList: { az: "Qara siyahıya sal", ru: "В чёрный список", en: "Add to blacklist" },
  undoBlackList: { az: "Qara siyahıdan çıxar", ru: "Убрать из чёрного списка", en: "Remove from blacklist" },
  blackListed: { az: "Qara siyahıda", ru: "В чёрном списке", en: "Blacklisted" },
  blackListReason: { az: "Səbəb (istəyə bağlı)", ru: "Причина (необязательно)", en: "Reason (optional)" },
  blackListNote: {
    az: "Qara siyahıdakı müştəri üçün süni intellekt görüş yazmır.",
    ru: "ИИ-агент не записывает клиентов из чёрного списка.",
    en: "The AI agent does not book appointments for blacklisted clients.",
  },

  // ---- Call Log ----
  callLog: { az: "Zəng jurnalı", ru: "Журнал звонков", en: "Call log" },
  allClassifications: { az: "Bütün növlər", ru: "Все типы", en: "All types" },
  allOutcomes: { az: "Bütün nəticələr", ru: "Все результаты", en: "All outcomes" },
  colDateTime: { az: "Tarix/vaxt", ru: "Дата/время", en: "Date/time" },
  colClient: { az: "Müştəri", ru: "Клиент", en: "Client" },
  colClassification: { az: "Növ", ru: "Тип", en: "Type" },
  colOutcome: { az: "Nəticə", ru: "Результат", en: "Outcome" },
  colDurationShort: { az: "Müddət", ru: "Длительность", en: "Duration" },
  colTurns: { az: "Növbələr", ru: "Реплики", en: "Turns" },
  // "Sual/cavab" — questions the caller asked over answers the agent gave, shown as "4/5".
  colQuestionsAnswers: { az: "Sual/cavab", ru: "Вопр./отв.", en: "Q/A" },
  colPipeline: { az: "Rejim", ru: "Режим", en: "Mode" },
  allPipelines: { az: "Bütün rejimlər", ru: "Все режимы", en: "All modes" },
  spendByPipeline: { az: "Rejimlər üzrə xərc", ru: "Расходы по режимам", en: "Spend by mode" },
  colCalls: { az: "Zənglər", ru: "Звонки", en: "Calls" },
  colModels: { az: "Modellər", ru: "Модели", en: "Models" },
  colAvgDuration: { az: "Orta müddət", ru: "Средняя длительность", en: "Avg. duration" },
  colCost: { az: "Dəyər", ru: "Стоимость", en: "Cost" },
  colCostPerMinute: { az: "Dəqiqəyə", ru: "За минуту", en: "Per minute" },
  noCallsMatchFilters: {
    az: "Bu filtrlərə uyğun zəng yoxdur.",
    ru: "Нет звонков, соответствующих фильтрам.",
    en: "No calls match these filters.",
  },
  noCallsYet: { az: "Hələ zəng yoxdur.", ru: "Пока нет звонков.", en: "No calls yet." },
  rowClickToCallDetail: {
    az: "Sətrə klikləyin → Zəng detalları.",
    ru: "Нажмите на строку → Детали звонка.",
    en: "Click a row → call details.",
  },
  failedToLoadCalls: { az: "Zənglər yüklənmədi.", ru: "Не удалось загрузить звонки.", en: "Couldn't load calls." },

  // ---- Call Detail ----
  turnsToResolution: { az: "Həllə qədər növbələr", ru: "Реплик до решения", en: "Turns to resolution" },
  callerQuestions: { az: "Müştəri sualları", ru: "Вопросы клиента", en: "Caller questions" },
  recording: { az: "Yazı", ru: "Запись", en: "Recording" },
  audioPlay: { az: "Oxut", ru: "Воспроизвести", en: "Play" },
  audioPause: { az: "Fasilə ver", ru: "Пауза", en: "Pause" },

  // ---- Call cost ----
  callCost: { az: "Zəngin dəyəri", ru: "Стоимость звонка", en: "Call cost" },
  costPerMinute: { az: "Dəqiqəyə düşən dəyər", ru: "Стоимость за минуту", en: "Cost per minute" },
  costPerAnswer: { az: "Bir cavaba düşən dəyər", ru: "Стоимость за ответ", en: "Cost per answer" },
  agentModel: { az: "Model", ru: "Модель", en: "Model" },
  tokenBreakdown: { az: "Token bölgüsü", ru: "Разбивка токенов", en: "Token breakdown" },
  totalTokens: { az: "Ümumi tokenlər", ru: "Всего токенов", en: "Total tokens" },
  tokensTextIn: { az: "Mətn (giriş)", ru: "Текст (вход)", en: "Text (in)" },
  tokensAudioIn: { az: "Səs (giriş)", ru: "Аудио (вход)", en: "Audio (in)" },
  tokensCachedTextIn: { az: "Keşlənmiş mətn (giriş)", ru: "Кешированный текст (вход)", en: "Cached text (in)" },
  tokensCachedAudioIn: { az: "Keşlənmiş səs (giriş)", ru: "Кешированное аудио (вход)", en: "Cached audio (in)" },
  tokensTextOut: { az: "Mətn (çıxış)", ru: "Текст (выход)", en: "Text (out)" },
  tokensAudioOut: { az: "Səs (çıxış)", ru: "Аудио (выход)", en: "Audio (out)" },
  costNotTracked: {
    az: "Bu zəng dəyər uçotu əlavə edilməzdən əvvəl qeydə alınıb.",
    ru: "Этот звонок записан до того, как был добавлён учёт стоимости.",
    en: "This call was logged before cost tracking was added.",
  },
  backToCallLog: { az: "Zəng jurnalına qayıt", ru: "Назад к журналу звонков", en: "Back to call log" },
  failedToLoadCallDetail: {
    az: "Zəng detalları yüklənmədi.",
    ru: "Не удалось загрузить детали звонка.",
    en: "Couldn't load call details.",
  },

  // ---- Dashboard ----
  kpiDashboard: { az: "Göstəricilər paneli", ru: "Панель показателей", en: "Dashboard" },
  rangeWeek: { az: "Bu həftə", ru: "На этой неделе", en: "This week" },
  rangeMonth: { az: "Bu ay", ru: "В этом месяце", en: "This month" },
  totalCalls: { az: "Ümumi zənglər", ru: "Всего звонков", en: "Total calls" },
  resolvedByAgent: {
    az: "Süni intellekt tərəfindən həll edilib",
    ru: "Решено ИИ-агентом",
    en: "Resolved by the AI agent",
  },
  escalationRate: { az: "Yönləndirmə nisbəti", ru: "Доля переадресаций", en: "Escalation rate" },
  avgCallDuration: { az: "Orta zəng müddəti", ru: "Средняя длительность звонка", en: "Avg. call duration" },
  callsSuffix: { az: "zəng", ru: "звонков", en: "calls" },
  avgTurnsSuffix: { az: "orta növbə", ru: "среднее число реплик", en: "avg. turns" },
  escalationOutcomes: { az: "Yönləndirmə nəticələri", ru: "Результаты переадресаций", en: "Escalation outcomes" },
  failureBreakdown: { az: "Uğursuzluqların bölgüsü", ru: "Разбивка неудач", en: "Failure breakdown" },
  resolvedByStaff: { az: "İşçi tərəfindən həll edilib", ru: "Решено сотрудником", en: "Resolved by staff" },
  abandonedNoPickup: { az: "Cavabsız qalıb", ru: "Не принят вовремя", en: "Abandoned — not picked up" },
  noCalendarAvailability: {
    az: "Təqvimdə boş vaxt yoxdur",
    ru: "Нет свободного времени в календаре",
    en: "No calendar availability",
  },
  agentLimitation: {
    az: "Süni intellektin imkan məhdudiyyəti",
    ru: "Ограничение возможностей ИИ",
    en: "AI agent limitation",
  },
  appointmentVolume: { az: "Görüşlərin sayı", ru: "Количество записей", en: "Appointment volume" },
  bookedInThisRange: { az: "bu dövrdə qeydə alınıb", ru: "записано за этот период", en: "booked in this range" },
  reminderNoAnswerRate: {
    az: "Xatırlatma zənglərinə cavabsızlıq nisbəti",
    ru: "Доля неотвеченных напоминаний",
    en: "Reminder no-answer rate",
  },
  ofReminderCallsInRange: {
    az: "bu dövrdəki xatırlatma zənglərinin",
    ru: "от напоминаний за этот период",
    en: "of reminder calls in this range",
  },
  failedToLoadDashboard: {
    az: "Göstəricilər yüklənmədi.",
    ru: "Не удалось загрузить показатели.",
    en: "Couldn't load the dashboard.",
  },
  agentSpend: { az: "Süni intellekt xərcləri", ru: "Расходы на ИИ-агента", en: "AI agent spend" },
  totalSpend: { az: "Ümumi xərc", ru: "Общие расходы", en: "Total spend" },
  avgCostPerCall: { az: "Zəngə düşən orta dəyər", ru: "Средняя стоимость звонка", en: "Avg. cost per call" },
  perMinuteSuffix: { az: "dəqiqəyə", ru: "за минуту", en: "per minute" },
  perAnswerSuffix: { az: "bir cavaba", ru: "за ответ", en: "per answer" },
  tokensSuffix: { az: "token", ru: "токенов", en: "tokens" },

  // ---- Language switch ----
  // The control shows "AZ", "RU" and "EN" literally, so it needs no translated label of its own.
  backToHome: { az: "Ana səhifə", ru: "Главная", en: "Home" },

  // ---- Theme switch ----
  theme: { az: "Mövzu", ru: "Тема", en: "Theme" },
  appearance: { az: "Görünüş", ru: "Оформление", en: "Appearance" },
  themeHint: {
    az: "Rəng mövzusu yalnız bu brauzerdə saxlanılır və bütün səhifələrə tətbiq olunur.",
    ru: "Цветовая тема сохраняется только в этом браузере и применяется ко всем страницам.",
    en: "The colour theme is stored in this browser only and applies to every page.",
  },

  // ---- Week calendar grid ----
  dayMon: { az: "B.e", ru: "Пн", en: "Mon" },
  dayTue: { az: "Ç.a", ru: "Вт", en: "Tue" },
  dayWed: { az: "Çər", ru: "Ср", en: "Wed" },
  dayThu: { az: "C.a", ru: "Чт", en: "Thu" },
  dayFri: { az: "Cüm", ru: "Пт", en: "Fri" },
  daySat: { az: "Şən", ru: "Сб", en: "Sat" },
  daySun: { az: "Baz", ru: "Вс", en: "Sun" },

  // ---- Escalation overlay ----
  incomingEscalatedCall: {
    az: "Daxil olan — yönləndirilmiş zəng",
    ru: "Входящий — переадресованный звонок",
    en: "Incoming — escalated call",
  },
  callerLabel: { az: "Zəng edən:", ru: "Звонящий:", en: "Caller:" },
  reasonLabel: { az: "Səbəb:", ru: "Причина:", en: "Reason:" },
  acceptAndConnectMic: {
    az: "Qəbul et və mikrofonu qoş",
    ru: "Принять и подключить микрофон",
    en: "Accept and connect microphone",
  },
  dismiss: { az: "Rədd et", ru: "Отклонить", en: "Dismiss" },
  liveConnected: { az: "Canlı — qoşulub", ru: "В эфире — подключено", en: "Live — connected" },
  callTimeLabel: { az: "Zəng müddəti", ru: "Длительность звонка", en: "Call time" },
  endCall: { az: "Zəngi bitir", ru: "Завершить звонок", en: "End call" },
  missed: { az: "Buraxılmış", ru: "Пропущено", en: "Missed" },
  noOneAcceptedInTime: {
    az: "Vaxtında heç kim qəbul etmədi — Yönləndirilib → cavabsız qalıb kimi qeyd edildi.",
    ru: "Никто не принял звонок вовремя — записано как «переадресовано → без ответа».",
    en: "Nobody accepted in time — logged as escalated → abandoned.",
  },
} satisfies Record<string, Dict>;

export type TranslationKey = keyof typeof translations;

// ---- Enum → display label maps (backend enum names are English identifiers) ----

export const appointmentStatusLabels: Record<string, Dict> = {
  Pending: { az: "Gözləmədə", ru: "Ожидание", en: "Pending" },
  Confirmed: { az: "Təsdiqlənib", ru: "Подтверждено", en: "Confirmed" },
  Cancelled: { az: "Ləğv edilib", ru: "Отменено", en: "Cancelled" },
};

/** Theme ids from theme/themes.ts. */
export const themeLabels: Record<string, Dict> = {
  ledger: { az: "Klassik — açıq", ru: "Классическая — светлая", en: "Classic — light" },
  dark: { az: "Klassik — tünd", ru: "Классическая — тёмная", en: "Classic — dark" },
  forest: { az: "Yaşıl", ru: "Зелёная", en: "Green" },
  graphite: { az: "Tünd boz — sarı", ru: "Тёмно-серая — жёлтая", en: "Dark grey — yellow" },
  ocean: { az: "Mavi", ru: "Синяя", en: "Blue" },
};

export const accountStatusLabels: Record<string, Dict> = {
  Active: { az: "Aktiv", ru: "Активен", en: "Active" },
  Inactive: { az: "Deaktiv", ru: "Неактивен", en: "Inactive" },
};

/** Module names for the admin grant screen. The landing page's moduleXxxTitle keys say the same
 *  things, but they are page copy — this map is keyed by the wire value the API sends, so the
 *  admin screen can render a switch per module without a lookup table of its own. */
export const moduleLabels: Record<string, Dict> = {
  Appointment: { az: "Randevu", ru: "Запись на приём", en: "Appointments" },
  Information: { az: "Məlumat xətti", ru: "Информационная линия", en: "Information line" },
  Reminder: { az: "Xatırlatma", ru: "Напоминания", en: "Reminders" },
  Feedback: { az: "Rəy və məmnuniyyət", ru: "Отзывы и удовлетворённость", en: "Feedback" },
  Order: { az: "Sifariş qəbulu", ru: "Приём заказов", en: "Order taking" },
  Survey: { az: "Sorğu və araşdırma", ru: "Опросы и исследования", en: "Surveys" },
};

export const accountRoleLabels: Record<string, Dict> = {
  PlatformAdmin: { az: "Platforma admini", ru: "Администратор платформы", en: "Platform admin" },
  Owner: { az: "Sahibkar", ru: "Владелец", en: "Owner" },
  Staff: { az: "İşçi", ru: "Сотрудник", en: "Staff" },
  Agent: { az: "Süni intellekt", ru: "ИИ-агент", en: "AI agent" },
};

export const callClassificationLabels: Record<string, Dict> = {
  NewAppointment: { az: "Yeni görüş", ru: "Новая запись", en: "New appointment" },
  UpdateReschedule: { az: "Dəyişiklik/təxirə salma", ru: "Изменение/перенос", en: "Change/reschedule" },
  Cancellation: { az: "Ləğv etmə", ru: "Отмена", en: "Cancellation" },
  ReminderConfirmation: { az: "Xatırlatma təsdiqi", ru: "Подтверждение напоминания", en: "Reminder confirmation" },
  InquiryOther: { az: "Sorğu/digər", ru: "Вопрос/другое", en: "Inquiry/other" },
};

export const callOutcomeLabels: Record<string, Dict> = {
  ResolvedByAgent: {
    az: "Süni intellekt tərəfindən həll edilib",
    ru: "Решено ИИ-агентом",
    en: "Resolved by the AI agent",
  },
  EscalatedResolvedByStaff: {
    az: "Yönləndirilib → işçi həll edib",
    ru: "Переадресовано → решено сотрудником",
    en: "Escalated → resolved by staff",
  },
  EscalatedAbandoned: {
    az: "Yönləndirilib → cavabsız qalıb",
    ru: "Переадресовано → без ответа",
    en: "Escalated → abandoned",
  },
  FailedNoAvailability: {
    az: "Uğursuz — boş vaxt yoxdur",
    ru: "Неудача — нет свободного времени",
    en: "Failed — no availability",
  },
  FailedAgentLimitation: {
    az: "Uğursuz — süni intellektin imkanı çatmayıb",
    ru: "Неудача — ограничение ИИ",
    en: "Failed — AI agent limitation",
  },
  NoAnswer: { az: "Cavab yoxdur", ru: "Нет ответа", en: "No answer" },
};

export function translateEnum(map: Record<string, Dict>, value: string, language: Language): string {
  return map[value]?.[language] ?? value;
}
