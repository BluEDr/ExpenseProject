import { useEffect, useMemo, useState, startTransition } from "react";
import "./App.css";
import logo from "./assets/logo.png";
import { authRequest, clearSession, createApiClient, loadSession, saveSession } from "./api";

const emptyExpense = {
  amount: "",
  date: todayValue(),
  note: "",
  categoryId: "",
  status: "Confirmed",
};

const emptyIncome = {
  amount: "",
  date: todayValue(),
  note: "",
  categoryId: "",
  status: "Confirmed",
};

const emptyIncomeSource = {
  name: "",
  monthlyAmount: "",
  startDate: todayValue(),
  endDate: "",
  note: "",
};

const defaultDashboard = {
  expenses: [],
  incomes: [],
  incomeSources: [],
  expenseMeta: { totalCount: 0, pageCount: 0 },
  incomeMeta: { totalCount: 0, pageCount: 0 },
  monthSummary: null,
  runningDay: null,
  todaySummary: null,
};

function App() {
  const [session, setSessionState] = useState(() => loadSession());
  const [authMode, setAuthMode] = useState("login");
  const [authForm, setAuthForm] = useState({
    email: "",
    password: "",
    currencyCode: "EUR",
    timeZoneId: "Europe/Athens",
  });
  const [month, setMonth] = useState(currentYearMonth());
  const [dayNumber, setDayNumber] = useState(currentDayNumber());
  const [expenseForm, setExpenseForm] = useState(emptyExpense);
  const [incomeForm, setIncomeForm] = useState(emptyIncome);
  const [incomeSourceForm, setIncomeSourceForm] = useState(emptyIncomeSource);
  const [editingExpenseId, setEditingExpenseId] = useState(null);
  const [editingIncomeId, setEditingIncomeId] = useState(null);
  const [editingIncomeSourceId, setEditingIncomeSourceId] = useState(null);
  const [dashboard, setDashboard] = useState(defaultDashboard);
  const [feedback, setFeedback] = useState({ type: "", message: "" });
  const [authBusy, setAuthBusy] = useState(false);
  const [mutationBusy, setMutationBusy] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const [quickAmount, setQuickAmount] = useState("");
  const [quickNote, setQuickNote] = useState("");
  const [menuOpen, setMenuOpen] = useState(false);
  const [quickStatus, setQuickStatus] = useState("");
  const [revealedMetrics, setRevealedMetrics] = useState(() => ({
    monthlyBalance: false,
    dailyBalance: false,
    allowedToday: false,
    spentSoFar: false,
    todayCount: false,
  }));
  const [theme, setTheme] = useState(() => loadTheme());

  const today = useMemo(() => todayValue(), []);
  const todayLabel = useMemo(
    () =>
      new Intl.DateTimeFormat("en-GB", {
        weekday: "short",
        day: "numeric",
        month: "short",
      }).format(new Date()),
    [],
  );

  useEffect(() => {
    setMenuOpen(false);
  }, [month, dayNumber, session]);

  useEffect(() => {
    setRevealedMetrics({
      monthlyBalance: false,
      dailyBalance: false,
      allowedToday: false,
      spentSoFar: false,
      todayCount: false,
    });
  }, [month, dayNumber, session]);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("expense-project-theme", theme);
  }, [theme]);

  function setSession(nextSession) {
    setSessionState(nextSession);
    if (nextSession) {
      saveSession(nextSession);
    } else {
      clearSession();
    }
  }

  const api = createApiClient(
    () => session,
    setSession,
    () => {
      setSession(null);
      setFeedback({ type: "error", message: "Session expired. Please log in again." });
    },
  );

  useEffect(() => {
    if (!session?.accessToken) {
      return;
    }

    let cancelled = false;

    async function loadDashboard() {
      setFeedback({ type: "", message: "" });

      try {
        const [incomes, incomeSources, monthSummary, runningDay, todaySummary] = await Promise.all([
          api.get("/api/v1/incomes?limit=8&offset=0"),
          api.get("/api/v1/income-sources"),
          api.get(`/api/v1/summaries/${month}`),
          api.get(`/api/v1/summaries/${month}/day/${dayNumber}`),
          api.get(`/api/v1/expenses/summary?from=${today}&to=${today}`),
        ]);

        if (cancelled) {
          return;
        }

        startTransition(() => {
          setDashboard({
            expenses: monthSummary.expenses ?? [],
            incomes: incomes.items ?? [],
            incomeSources,
            expenseMeta: {
              totalCount: monthSummary.expenseCount ?? 0,
              pageCount: monthSummary.expenses?.length ?? 0,
            },
            incomeMeta: { totalCount: incomes.totalCount ?? 0, pageCount: incomes.pageCount ?? 0 },
            monthSummary,
            runningDay,
            todaySummary,
          });
        });
      } catch (error) {
        if (!cancelled) {
          setFeedback({ type: "error", message: error.message });
        }
      } finally {
        if (!cancelled) {
        }
      }
    }

    loadDashboard();

    return () => {
      cancelled = true;
    };
  }, [dayNumber, month, reloadKey, session, today]);

  async function handleAuthSubmit(event) {
    event.preventDefault();
    setAuthBusy(true);
    setFeedback({ type: "", message: "" });

    try {
      const path = authMode === "login" ? "/api/v1/auth/login" : "/api/v1/auth/register";
      const payload =
        authMode === "login"
          ? { email: authForm.email, password: authForm.password }
          : authForm;

      const result = await authRequest(path, payload);
      setSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        expiresAtUtc: result.expiresAtUtc,
        email: authForm.email,
      });
      setFeedback({ type: "success", message: `${authMode === "login" ? "Logged in" : "Registered"} successfully.` });
    } catch (error) {
      setFeedback({ type: "error", message: error.message });
    } finally {
      setAuthBusy(false);
    }
  }

  async function handleCreateExpense(event) {
    event.preventDefault();
    const payload = {
      categoryId: trimToNull(expenseForm.categoryId),
      amount: Number(expenseForm.amount),
      date: expenseForm.date,
      note: trimToNull(expenseForm.note),
    };

    if (editingExpenseId) {
      await submitEntity(
        `/api/v1/expenses/${editingExpenseId}`,
        payload,
        "Expense updated.",
        () => {
          setExpenseForm(emptyExpense);
          setEditingExpenseId(null);
        },
        "put",
      );
      return;
    }

    await submitEntity("/api/v1/expenses", payload, "Expense added.", () => setExpenseForm(emptyExpense));
  }

  async function handleQuickExpenseSubmit() {
    if (!quickAmount || Number(quickAmount) <= 0) {
      setFeedback({ type: "error", message: "Enter an amount before adding the expense." });
      return;
    }

    const payload = {
      categoryId: trimToNull(expenseForm.categoryId),
      amount: Number(quickAmount),
      date: editingExpenseId ? expenseForm.date : today,
      note: trimToNull(quickNote),
      status: expenseForm.status,
    };

    if (editingExpenseId) {
      await submitEntity(
        `/api/v1/expenses/${editingExpenseId}`,
        payload,
        "Expense updated from numpad.",
        () => {
          setQuickAmount("");
          setQuickNote("");
          setExpenseForm(emptyExpense);
          setEditingExpenseId(null);
          setQuickStatus("OK. Expense updated.");
        },
        "put",
      );
      return;
    }

    await submitEntity(
      "/api/v1/expenses",
      payload,
      "Expense added from numpad.",
      () => {
        setQuickAmount("");
        setQuickNote("");
        setQuickStatus("OK. Expense added.");
      },
    );
  }

  async function handleCreateIncome(event) {
    event.preventDefault();
    const payload = {
      categoryId: trimToNull(incomeForm.categoryId),
      amount: Number(incomeForm.amount),
      date: incomeForm.date,
      note: trimToNull(incomeForm.note),
      status: incomeForm.status,
    };

    if (editingIncomeId) {
      await submitEntity(
        `/api/v1/incomes/${editingIncomeId}`,
        payload,
        "Income updated.",
        () => {
          setIncomeForm(emptyIncome);
          setEditingIncomeId(null);
        },
        "put",
      );
      return;
    }

    await submitEntity("/api/v1/incomes", payload, "Income added.", () => setIncomeForm(emptyIncome));
  }

  async function handleCreateIncomeSource(event) {
    event.preventDefault();
    const payload = {
      name: incomeSourceForm.name.trim(),
      monthlyAmount: Number(incomeSourceForm.monthlyAmount),
      startDate: incomeSourceForm.startDate,
      endDate: trimToNull(incomeSourceForm.endDate),
      note: trimToNull(incomeSourceForm.note),
    };

    if (editingIncomeSourceId) {
      await submitEntity(
        `/api/v1/income-sources/${editingIncomeSourceId}`,
        payload,
        "Income source updated.",
        () => {
          setIncomeSourceForm(emptyIncomeSource);
          setEditingIncomeSourceId(null);
        },
        "put",
      );
      return;
    }

    await submitEntity("/api/v1/income-sources", payload, "Income source added.", () => setIncomeSourceForm(emptyIncomeSource));
  }

  async function submitEntity(path, payload, message, resetForm, method = "post") {
    setMutationBusy(true);
    setFeedback({ type: "", message: "" });
    setQuickStatus("");

    try {
      if (method === "put") {
        await api.put(path, payload);
      } else {
        await api.post(path, payload);
      }
      resetForm();
      setFeedback({ type: "success", message });
      setReloadKey((value) => value + 1);
    } catch (error) {
      setFeedback({ type: "error", message: error.message });
    } finally {
      setMutationBusy(false);
    }
  }

  async function handleDelete(path, message) {
    setMutationBusy(true);
    setFeedback({ type: "", message: "" });

    try {
      await api.delete(path);
      setFeedback({ type: "success", message });
      setReloadKey((value) => value + 1);
    } catch (error) {
      setFeedback({ type: "error", message: error.message });
    } finally {
      setMutationBusy(false);
    }
  }

  async function handleLogout() {
    try {
      if (session?.refreshToken) {
        await authRequest("/api/v1/auth/logout", { refreshToken: session.refreshToken });
      }
    } catch {
      // Ignore logout transport errors and clear local session anyway.
    }

    setSession(null);
    setDashboard(defaultDashboard);
    setQuickAmount("");
    setQuickNote("");
    setExpenseForm(emptyExpense);
    setIncomeForm(emptyIncome);
    setIncomeSourceForm(emptyIncomeSource);
    setEditingExpenseId(null);
    setEditingIncomeId(null);
    setEditingIncomeSourceId(null);
    setMenuOpen(false);
    setQuickStatus("");
  }

  function handleNumpadInput(key) {
    setQuickAmount((current) => {
      if (key === "clear") {
        return "";
      }

      if (key === "backspace") {
        return current.slice(0, -1);
      }

      if (key === ".") {
        return current.includes(".") ? current : `${current || "0"}.`;
      }

      if (current === "0") {
        return key;
      }

      return `${current}${key}`;
    });
  }

  function revealMetric(key) {
    setRevealedMetrics((current) => ({ ...current, [key]: true }));
  }

  function startExpenseEdit(item) {
    setEditingExpenseId(item.id);
    setQuickAmount(`${item.amount ?? ""}`);
    setQuickNote(item.note ?? "");
    setExpenseForm({
      amount: `${item.amount ?? ""}`,
      date: item.date,
      note: item.note ?? "",
      categoryId: item.categoryId ?? "",
      status: item.status ?? "Confirmed",
    });
    setFeedback({ type: "success", message: "Expense loaded into quick edit." });
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function startIncomeEdit(item) {
    setEditingIncomeId(item.id);
    setIncomeForm({
      amount: `${item.amount ?? ""}`,
      date: item.date,
      note: item.note ?? "",
      categoryId: item.categoryId ?? "",
      status: item.status ?? "Confirmed",
    });
    setFeedback({ type: "success", message: "Income loaded for editing." });
  }

  function startIncomeSourceEdit(item) {
    setEditingIncomeSourceId(item.id);
    setIncomeSourceForm({
      name: item.name ?? "",
      monthlyAmount: `${item.monthlyAmount ?? ""}`,
      startDate: item.startDate,
      endDate: item.endDate ?? "",
      note: item.note ?? "",
    });
    setFeedback({ type: "success", message: "Income source loaded for editing." });
  }

  if (!session?.accessToken) {
    return (
      <main className="shell auth-shell">
        <section className="auth-panel">
          <div className="brand-lockup auth-brand-lockup">
            <img className="brand-logo" src={logo} alt="Expense Manager logo" />
            <div className="brand-copy">
              <p className="brand-title">Expense Manager</p>
            </div>
          </div>
          <h1>Control cash flow without leaving the month blind.</h1>
          <p className="lede">
            This client talks directly to your ASP.NET API. Log in, then manage expenses,
            incomes, recurring salary sources, and month-by-month runway from one screen.
          </p>

          <div className="auth-toggle">
            <button
              className={authMode === "login" ? "active" : ""}
              type="button"
              onClick={() => setAuthMode("login")}
            >
              Login
            </button>
            <button
              className={authMode === "register" ? "active" : ""}
              type="button"
              onClick={() => setAuthMode("register")}
            >
              Register
            </button>
          </div>

          <form className="panel form-grid" onSubmit={handleAuthSubmit}>
            <label>
              Email
              <input
                type="email"
                value={authForm.email}
                onChange={(event) => setAuthForm({ ...authForm, email: event.target.value })}
                required
              />
            </label>
            <label>
              Password
              <input
                type="password"
                value={authForm.password}
                onChange={(event) => setAuthForm({ ...authForm, password: event.target.value })}
                required
              />
            </label>
            {authMode === "register" && (
              <>
                <label>
                  Currency
                  <input
                    value={authForm.currencyCode}
                    onChange={(event) =>
                      setAuthForm({ ...authForm, currencyCode: event.target.value })
                    }
                  />
                </label>
                <label>
                  Time zone
                  <input
                    value={authForm.timeZoneId}
                    onChange={(event) =>
                      setAuthForm({ ...authForm, timeZoneId: event.target.value })
                    }
                  />
                </label>
              </>
            )}
            <button className="primary" type="submit" disabled={authBusy}>
              {authBusy ? "Working..." : authMode === "login" ? "Login" : "Create account"}
            </button>
          </form>

          {feedback.message && <p className={`feedback ${feedback.type}`}>{feedback.message}</p>}
        </section>
      </main>
    );
  }

  return (
    <main className="shell shell-mobile-first">
      <header className="topbar topbar-mobile">
        <div className="brand-lockup">
          <img className="brand-logo" src={logo} alt="Expense Manager logo" />
          <div className="brand-copy">
            <p className="brand-title">Expense Manager</p>
            <p className="muted">{todayLabel}</p>
          </div>
        </div>
        <div className="menu-anchor">
          <button
            type="button"
            className="burger-button"
            aria-label="Open account menu"
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((value) => !value)}
          >
            <span />
            <span />
            <span />
          </button>
          {menuOpen ? (
            <div className="burger-menu">
              <p className="eyebrow">Account</p>
              <strong>{session.email}</strong>
              <span className="muted">Month {formatYearMonth(month)}</span>
              <button
                type="button"
                className="theme-toggle"
                onClick={() => setTheme((current) => (current === "light" ? "dark" : "light"))}
              >
                {theme === "light" ? "Switch to dark theme" : "Switch to light theme"}
              </button>
              <button type="button" onClick={handleLogout}>
                Logout
              </button>
            </div>
          ) : null}
        </div>
      </header>

      {feedback.message && <p className={`feedback ${feedback.type}`}>{feedback.message}</p>}

      <section className="mobile-home-grid mobile-home-grid-priority">
        <article className="panel quick-add-panel quick-add-panel-priority">
          <div className="numpad-display-wrap">
            <span className="numpad-caption">Amount for {todayLabel}</span>
            <input
              className="numpad-display"
              value={quickAmount ? formatMoney(quickAmount) : "EUR 0.00"}
              readOnly
              inputMode="none"
              aria-label="Quick expense amount"
            />
          </div>

          <div className="hero-metrics-grid hero-metrics-grid-compact">
            <MetricCard
              title="Running delta"
              value={dashboard.runningDay?.net}
              tone={metricTone(dashboard.runningDay?.net)}
            />
            <MetricCard title="Expenses today" value={dashboard.todaySummary?.total} negative />
            <PrivacyMetricCard
              title="Monthly balance"
              value={dashboard.monthSummary?.monthlyBalance}
              revealed={revealedMetrics.monthlyBalance}
              onReveal={() => revealMetric("monthlyBalance")}
              emphasis
            />
            <PrivacyMetricCard
              title="Daily balance"
              value={dashboard.monthSummary?.dailyAllowance}
              revealed={revealedMetrics.dailyBalance}
              onReveal={() => revealMetric("dailyBalance")}
            />
          </div>

          <div className="numpad-grid">
            {["7", "8", "9", "4", "5", "6", "1", "2", "3", ".", "0"].map((key) => (
              <button
                key={key}
                type="button"
                className="numpad-key"
                onClick={() => handleNumpadInput(key)}
              >
                {key}
              </button>
            ))}
            <button type="button" className="numpad-key numpad-key-accent" onClick={() => handleNumpadInput("backspace")}>
              Del
            </button>
          </div>

          <label className="quick-note-field">
            <input
              type="text"
              value={quickNote}
              onChange={(event) => setQuickNote(event.target.value)}
              placeholder="Optional note"
              aria-label="Expense note"
            />
          </label>

          <div className="quick-actions">
            <button type="button" className="primary quick-submit" disabled={mutationBusy} onClick={handleQuickExpenseSubmit}>
              {mutationBusy ? "Saving..." : editingExpenseId ? "Update expense" : "Add expense"}
            </button>
          </div>
          {quickStatus ? <p className="quick-status">{quickStatus}</p> : null}
        </article>

        <DataPanel
          title={`Month expenses (${dashboard.expenseMeta.pageCount}/${dashboard.expenseMeta.totalCount})`}
          items={dashboard.expenses}
          emptyText="No confirmed expenses for this month."
          onDelete={(item) => handleDelete(`/api/v1/expenses/${item.id}`, "Expense archived.")}
          onEdit={startExpenseEdit}
          renderItem={(item) => (
            <>
              <strong>{formatMoney(item.amount)}</strong>
              <span>{item.date}</span>
              <span>{item.note || "No note"}</span>
              <span>{item.status}</span>
            </>
          )}
        />

        <article className="panel mobile-hero">
          <div className="mobile-hero-header">
          <div>
            <p className="eyebrow">Control panel</p>
            <h2>{formatYearMonth(month)}</h2>
          </div>
          <div className="summary-controls summary-controls-compact">
            <label>
              Month
              <input
                type="month"
                value={toMonthInput(month)}
                onChange={(event) => setMonth(fromMonthInput(event.target.value))}
              />
            </label>
            <label>
              Day
              <input
                type="number"
                min="1"
                max={dashboard.monthSummary?.daysInMonth ?? 31}
                value={dayNumber}
                onChange={(event) => setDayNumber(Number(event.target.value))}
              />
            </label>
          </div>
          </div>

          <div className="snapshot-strip">
            <PrivacySnapshotPill
              label="Opening balance"
              value={formatMoney(dashboard.monthSummary?.startingBalance)}
              revealed={revealedMetrics.allowedToday}
              onReveal={() => revealMetric("allowedToday")}
            />
            <PrivacySnapshotPill
              label="Previous close"
              value={formatMoney(dashboard.monthSummary?.previousMonthClosingBalance)}
              revealed={revealedMetrics.spentSoFar}
              onReveal={() => revealMetric("spentSoFar")}
            />
            <PrivacySnapshotPill
              label="Spent so far"
              value={formatMoney(dashboard.runningDay?.cumulativeExpenses)}
              revealed={revealedMetrics.todayCount}
              onReveal={() => revealMetric("todayCount")}
            />
          </div>
        </article>

        <article className="panel running-panel">
          <div className="panel-header panel-header-tight">
            <div>
              <p className="eyebrow">Running day</p>
              <h2>Budget pace</h2>
            </div>
          </div>
          {dashboard.runningDay ? (
            <dl className="day-grid day-grid-mobile">
              <SummaryRow label="Date" value={dashboard.runningDay.date} />
              <SummaryRow label="Expense today" value={formatMoney(dashboard.runningDay.expense)} />
              <SummaryRow label="Cumulative expenses" value={formatMoney(dashboard.runningDay.cumulativeExpenses)} />
              <SummaryRow label="Allowed until day" value={formatMoney(dashboard.runningDay.allowedUntilDay)} />
              <SummaryRow label="Running delta" value={formatMoney(dashboard.runningDay.net)} />
            </dl>
          ) : (
            <p className="muted">No running day summary loaded.</p>
          )}
        </article>
      </section>

      <section className="grid dashboard-secondary-grid">
        <FormPanel
          title="Add Expense"
          subtitle={editingExpenseId ? "Update expense" : "Detailed entry"}
          onSubmit={handleCreateExpense}
        >
          <label>
            Amount
            <input
              type="number"
              min="0"
              step="0.01"
              value={expenseForm.amount}
              onChange={(event) => setExpenseForm({ ...expenseForm, amount: event.target.value })}
              required
            />
          </label>
          <label>
            Date
            <input
              type="date"
              value={expenseForm.date}
              onChange={(event) => setExpenseForm({ ...expenseForm, date: event.target.value })}
              required
            />
          </label>
          <label>
            Category id
            <input
              value={expenseForm.categoryId}
              onChange={(event) => setExpenseForm({ ...expenseForm, categoryId: event.target.value })}
              placeholder="Optional GUID"
            />
          </label>
          <label>
            Note
            <textarea
              value={expenseForm.note}
              onChange={(event) => setExpenseForm({ ...expenseForm, note: event.target.value })}
            />
          </label>
          <button className="primary" type="submit" disabled={mutationBusy}>
            {editingExpenseId ? "Update expense" : "Add expense"}
          </button>
          {editingExpenseId ? (
            <button type="button" className="ghost" onClick={() => { setExpenseForm(emptyExpense); setEditingExpenseId(null); }}>
              Cancel edit
            </button>
          ) : null}
        </FormPanel>

        <FormPanel
          title="Add Income"
          subtitle={editingIncomeId ? "Update income" : "Confirmed cash flow"}
          onSubmit={handleCreateIncome}
        >
          <label>
            Amount
            <input
              type="number"
              min="0"
              step="0.01"
              value={incomeForm.amount}
              onChange={(event) => setIncomeForm({ ...incomeForm, amount: event.target.value })}
              required
            />
          </label>
          <label>
            Date
            <input
              type="date"
              value={incomeForm.date}
              onChange={(event) => setIncomeForm({ ...incomeForm, date: event.target.value })}
              required
            />
          </label>
          <label>
            Status
            <select
              value={incomeForm.status}
              onChange={(event) => setIncomeForm({ ...incomeForm, status: event.target.value })}
            >
              <option>Confirmed</option>
              <option>Pending</option>
              <option>Cancelled</option>
            </select>
          </label>
          <label>
            Category id
            <input
              value={incomeForm.categoryId}
              onChange={(event) => setIncomeForm({ ...incomeForm, categoryId: event.target.value })}
              placeholder="Optional GUID"
            />
          </label>
          <label>
            Note
            <textarea
              value={incomeForm.note}
              onChange={(event) => setIncomeForm({ ...incomeForm, note: event.target.value })}
            />
          </label>
          <button className="primary" type="submit" disabled={mutationBusy}>
            {editingIncomeId ? "Update income" : "Add income"}
          </button>
          {editingIncomeId ? (
            <button type="button" className="ghost" onClick={() => { setIncomeForm(emptyIncome); setEditingIncomeId(null); }}>
              Cancel edit
            </button>
          ) : null}
        </FormPanel>

        <FormPanel
          title="Add Income Source"
          subtitle={editingIncomeSourceId ? "Update recurring source" : "Recurring monthly source"}
          onSubmit={handleCreateIncomeSource}
        >
          <label>
            Name
            <input
              value={incomeSourceForm.name}
              onChange={(event) => setIncomeSourceForm({ ...incomeSourceForm, name: event.target.value })}
              required
            />
          </label>
          <label>
            Monthly amount
            <input
              type="number"
              min="0"
              step="0.01"
              value={incomeSourceForm.monthlyAmount}
              onChange={(event) => setIncomeSourceForm({ ...incomeSourceForm, monthlyAmount: event.target.value })}
              required
            />
          </label>
          <label>
            Start date
            <input
              type="date"
              value={incomeSourceForm.startDate}
              onChange={(event) => setIncomeSourceForm({ ...incomeSourceForm, startDate: event.target.value })}
              required
            />
          </label>
          <label>
            End date
            <input
              type="date"
              value={incomeSourceForm.endDate}
              onChange={(event) => setIncomeSourceForm({ ...incomeSourceForm, endDate: event.target.value })}
            />
          </label>
          <label>
            Note
            <textarea
              value={incomeSourceForm.note}
              onChange={(event) => setIncomeSourceForm({ ...incomeSourceForm, note: event.target.value })}
            />
          </label>
          <button className="primary" type="submit" disabled={mutationBusy}>
            {editingIncomeSourceId ? "Update source" : "Add source"}
          </button>
          {editingIncomeSourceId ? (
            <button type="button" className="ghost" onClick={() => { setIncomeSourceForm(emptyIncomeSource); setEditingIncomeSourceId(null); }}>
              Cancel edit
            </button>
          ) : null}
        </FormPanel>
      </section>

      <section className="grid data-grid">
        <DataPanel
          title={`Recent incomes (${dashboard.incomeMeta.pageCount}/${dashboard.incomeMeta.totalCount})`}
          items={dashboard.incomes}
          emptyText="No incomes yet."
          onDelete={(item) => handleDelete(`/api/v1/incomes/${item.id}`, "Income archived.")}
          onEdit={startIncomeEdit}
          renderItem={(item) => (
            <>
              <strong>{formatMoney(item.amount)}</strong>
              <span>{item.date}</span>
              <span>{item.status}</span>
            </>
          )}
        />

        <DataPanel
          title="Income sources"
          items={dashboard.incomeSources}
          emptyText="No recurring sources yet."
          onDelete={(item) => handleDelete(`/api/v1/income-sources/${item.id}`, "Income source archived.")}
          onEdit={startIncomeSourceEdit}
          renderItem={(item) => (
            <>
              <strong>{item.name}</strong>
              <span>{formatMoney(item.monthlyAmount)} / month</span>
              <span>
                {item.startDate} {item.endDate ? `to ${item.endDate}` : "onward"}
              </span>
            </>
          )}
        />
      </section>
    </main>
  );
}

function FormPanel({ title, subtitle, onSubmit, children }) {
  return (
    <article className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">{title}</p>
          <h2>{subtitle}</h2>
        </div>
      </div>
      <form className="form-grid" onSubmit={onSubmit}>
        {children}
      </form>
    </article>
  );
}

function DataPanel({ title, items, emptyText, renderItem, onDelete, onEdit }) {
  return (
    <article className="panel">
      <div className="panel-header">
        <h2>{title}</h2>
      </div>
      {items.length === 0 ? (
        <p className="muted">{emptyText}</p>
      ) : (
        <ul className="data-list">
          {items.map((item) => (
            <li key={item.id}>
              <div className="data-item">{renderItem(item)}</div>
              <div className="item-actions">
                {onEdit ? (
                  <button type="button" className="icon-button" aria-label="Edit item" title="Edit" onClick={() => onEdit(item)}>
                    <EditIcon />
                  </button>
                ) : null}
                <button
                  type="button"
                  className="icon-button icon-button-danger"
                  aria-label="Archive item"
                  title="Archive"
                  onClick={() => onDelete(item)}
                >
                  <DeleteIcon />
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

function EditIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 16.25V20h3.75L18.81 8.94l-3.75-3.75L4 16.25Z" fill="currentColor" />
      <path d="m20.71 7.04-3.75-3.75-1.42 1.41 3.75 3.75 1.42-1.41Z" fill="currentColor" />
    </svg>
  );
}

function DeleteIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 3h6l1 2h4v2H4V5h4l1-2Z" fill="currentColor" />
      <path d="M6 9h12l-1 11H7L6 9Z" fill="currentColor" />
    </svg>
  );
}

function MetricCard({ title, value, negative = false, emphasis = false, tone = "default" }) {
  return (
    <article className={`metric ${negative ? "metric-negative" : ""} ${emphasis ? "metric-emphasis" : ""} metric-${tone}`}>
      <span>{title}</span>
      <strong>{formatMoney(value)}</strong>
    </article>
  );
}

function PrivacyMetricCard({ title, value, revealed, onReveal, emphasis = false }) {
  if (revealed) {
    return <MetricCard title={title} value={value} emphasis={emphasis} />;
  }

  return (
    <button type="button" className={`metric metric-private ${emphasis ? "metric-emphasis" : ""}`} onClick={onReveal}>
      <span>{title}</span>
      <strong>Tap to reveal</strong>
    </button>
  );
}

function SnapshotPill({ label, value }) {
  return (
    <div className="snapshot-pill">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function PrivacySnapshotPill({ label, value, revealed, onReveal }) {
  if (revealed) {
    return <SnapshotPill label={label} value={value} />;
  }

  return (
    <button type="button" className="snapshot-pill snapshot-pill-private" onClick={onReveal}>
      <span>{label}</span>
      <strong>Tap to reveal</strong>
    </button>
  );
}

function SummaryRow({ label, value }) {
  return (
    <>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </>
  );
}

function trimToNull(value) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function formatMoney(value) {
  if (value == null || Number.isNaN(Number(value))) {
    return "EUR 0.00";
  }

  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "EUR",
  }).format(Number(value));
}

function currentYearMonth() {
  const now = new Date();
  const month = `${now.getMonth() + 1}`.padStart(2, "0");
  return `${now.getFullYear()}${month}`;
}

function currentDayNumber() {
  return new Date().getDate();
}

function toMonthInput(yearMonth) {
  return `${yearMonth.slice(0, 4)}-${yearMonth.slice(4, 6)}`;
}

function fromMonthInput(value) {
  return value.replace("-", "");
}

function formatYearMonth(yearMonth) {
  const date = new Date(Number(yearMonth.slice(0, 4)), Number(yearMonth.slice(4, 6)) - 1, 1);
  return new Intl.DateTimeFormat("en-US", {
    month: "long",
    year: "numeric",
  }).format(date);
}

function todayValue() {
  return new Date().toISOString().slice(0, 10);
}

function loadTheme() {
  if (typeof window === "undefined") {
    return "light";
  }

  const storedTheme = window.localStorage.getItem("expense-project-theme");
  return storedTheme === "dark" ? "dark" : "light";
}

function metricTone(value) {
  if (value == null || Number.isNaN(Number(value))) {
    return "default";
  }

  return Number(value) < 0 ? "danger" : "success";
}

export default App;







