import { useEffect, useState, startTransition } from "react";
import "./App.css";
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
  const [dashboard, setDashboard] = useState({
    expenses: [],
    incomes: [],
    incomeSources: [],
    expenseMeta: { totalCount: 0, pageCount: 0 },
    incomeMeta: { totalCount: 0, pageCount: 0 },
    monthSummary: null,
    runningDay: null,
  });
  const [feedback, setFeedback] = useState({ type: "", message: "" });
  const [busy, setBusy] = useState(false);

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
      setBusy(true);
      setFeedback({ type: "", message: "" });

      try {
        const [expenses, incomes, incomeSources, monthSummary, runningDay] = await Promise.all([
          api.get("/api/v1/expenses?limit=20&offset=0"),
          api.get("/api/v1/incomes?limit=20&offset=0"),
          api.get("/api/v1/income-sources"),
          api.get(`/api/v1/summaries/${month}`),
          api.get(`/api/v1/summaries/${month}/day/${dayNumber}`),
        ]);

        if (cancelled) {
          return;
        }

        startTransition(() => {
          setDashboard({
            expenses: expenses.items ?? [],
            incomes: incomes.items ?? [],
            incomeSources,
            expenseMeta: { totalCount: expenses.totalCount ?? 0, pageCount: expenses.pageCount ?? 0 },
            incomeMeta: { totalCount: incomes.totalCount ?? 0, pageCount: incomes.pageCount ?? 0 },
            monthSummary,
            runningDay,
          });
        });
      } catch (error) {
        if (!cancelled) {
          setFeedback({ type: "error", message: error.message });
        }
      } finally {
        if (!cancelled) {
          setBusy(false);
        }
      }
    }

    loadDashboard();

    return () => {
      cancelled = true;
    };
  }, [session, month, dayNumber]);

  async function handleAuthSubmit(event) {
    event.preventDefault();
    setBusy(true);
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
      setBusy(false);
    }
  }

  async function handleCreateExpense(event) {
    event.preventDefault();
    await submitEntity(
      "/api/v1/expenses",
      {
        categoryId: trimToNull(expenseForm.categoryId),
        amount: Number(expenseForm.amount),
        date: expenseForm.date,
        note: trimToNull(expenseForm.note),
      },
      "Expense added.",
      () => setExpenseForm(emptyExpense),
    );
  }

  async function handleCreateIncome(event) {
    event.preventDefault();
    await submitEntity(
      "/api/v1/incomes",
      {
        categoryId: trimToNull(incomeForm.categoryId),
        amount: Number(incomeForm.amount),
        date: incomeForm.date,
        note: trimToNull(incomeForm.note),
        status: incomeForm.status,
      },
      "Income added.",
      () => setIncomeForm(emptyIncome),
    );
  }

  async function handleCreateIncomeSource(event) {
    event.preventDefault();
    await submitEntity(
      "/api/v1/income-sources",
      {
        name: incomeSourceForm.name.trim(),
        monthlyAmount: Number(incomeSourceForm.monthlyAmount),
        startDate: incomeSourceForm.startDate,
        endDate: trimToNull(incomeSourceForm.endDate),
        note: trimToNull(incomeSourceForm.note),
      },
      "Income source added.",
      () => setIncomeSourceForm(emptyIncomeSource),
    );
  }

  async function submitEntity(path, payload, message, resetForm) {
    setBusy(true);
    setFeedback({ type: "", message: "" });

    try {
      await api.post(path, payload);
      resetForm();
      setFeedback({ type: "success", message });
      setSession({ ...session });
    } catch (error) {
      setFeedback({ type: "error", message: error.message });
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete(path, message) {
    setBusy(true);
    setFeedback({ type: "", message: "" });

    try {
      await api.delete(path);
      setFeedback({ type: "success", message });
      setSession({ ...session });
    } catch (error) {
      setFeedback({ type: "error", message: error.message });
    } finally {
      setBusy(false);
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
    setDashboard({
      expenses: [],
      incomes: [],
      incomeSources: [],
      expenseMeta: { totalCount: 0, pageCount: 0 },
      incomeMeta: { totalCount: 0, pageCount: 0 },
      monthSummary: null,
      runningDay: null,
    });
  }

  if (!session?.accessToken) {
    return (
      <main className="shell auth-shell">
        <section className="auth-panel">
          <p className="eyebrow">Expense Project</p>
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
            <button className="primary" type="submit" disabled={busy}>
              {busy ? "Working..." : authMode === "login" ? "Login" : "Create account"}
            </button>
          </form>

          {feedback.message && <p className={`feedback ${feedback.type}`}>{feedback.message}</p>}
        </section>
      </main>
    );
  }

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Expense Project</p>
          <h1>Operational budget console</h1>
        </div>
        <div className="topbar-actions">
          <span className="session-pill">{session.email}</span>
          <button type="button" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </header>

      {feedback.message && <p className={`feedback ${feedback.type}`}>{feedback.message}</p>}

      <section className="grid">
        <article className="panel panel-wide">
          <div className="panel-header">
            <div>
              <p className="eyebrow">Month Summary</p>
              <h2>{formatYearMonth(month)}</h2>
            </div>
            <div className="summary-controls">
              <label>
                Month
                <input
                  type="month"
                  value={toMonthInput(month)}
                  onChange={(event) => setMonth(fromMonthInput(event.target.value))}
                />
              </label>
              <label>
                Running day
                <input
                  type="number"
                  min="1"
                  max="31"
                  value={dayNumber}
                  onChange={(event) => setDayNumber(Number(event.target.value))}
                />
              </label>
            </div>
          </div>

          <div className="summary-grid">
            <MetricCard title="Recurring income" value={dashboard.monthSummary?.incomeSourcesTotal} />
            <MetricCard title="Extra income" value={dashboard.monthSummary?.incomesTotal} />
            <MetricCard title="Expenses" value={dashboard.monthSummary?.expensesTotal} negative />
            <MetricCard title="Monthly balance" value={dashboard.monthSummary?.monthlyBalance} />
            <MetricCard title="Daily allowance" value={dashboard.monthSummary?.dailyAllowance} />
            <MetricCard title="Running delta" value={dashboard.runningDay?.net} />
          </div>

          <div className="running-day">
            <h3>Running day snapshot</h3>
            {dashboard.runningDay ? (
              <dl className="day-grid">
                <SummaryRow label="Date" value={dashboard.runningDay.date} />
                <SummaryRow label="Day expense" value={formatMoney(dashboard.runningDay.expense)} />
                <SummaryRow
                  label="Cumulative expenses"
                  value={formatMoney(dashboard.runningDay.cumulativeExpenses)}
                />
                <SummaryRow
                  label="Allowed until day"
                  value={formatMoney(dashboard.runningDay.allowedUntilDay)}
                />
                <SummaryRow label="Net" value={formatMoney(dashboard.runningDay.net)} />
              </dl>
            ) : (
              <p className="muted">No running day summary loaded.</p>
            )}
          </div>
        </article>

        <FormPanel
          title="Add Expense"
          subtitle="Optional category, confirmed spending by default."
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
              onChange={(event) =>
                setExpenseForm({ ...expenseForm, categoryId: event.target.value })
              }
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
          <button className="primary" type="submit" disabled={busy}>
            Add expense
          </button>
        </FormPanel>

        <FormPanel
          title="Add Income"
          subtitle="Use confirmed for real cash flow."
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
              onChange={(event) =>
                setIncomeForm({ ...incomeForm, categoryId: event.target.value })
              }
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
          <button className="primary" type="submit" disabled={busy}>
            Add income
          </button>
        </FormPanel>

        <FormPanel
          title="Add Income Source"
          subtitle="Recurring monthly source such as salary."
          onSubmit={handleCreateIncomeSource}
        >
          <label>
            Name
            <input
              value={incomeSourceForm.name}
              onChange={(event) =>
                setIncomeSourceForm({ ...incomeSourceForm, name: event.target.value })
              }
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
              onChange={(event) =>
                setIncomeSourceForm({ ...incomeSourceForm, monthlyAmount: event.target.value })
              }
              required
            />
          </label>
          <label>
            Start date
            <input
              type="date"
              value={incomeSourceForm.startDate}
              onChange={(event) =>
                setIncomeSourceForm({ ...incomeSourceForm, startDate: event.target.value })
              }
              required
            />
          </label>
          <label>
            End date
            <input
              type="date"
              value={incomeSourceForm.endDate}
              onChange={(event) =>
                setIncomeSourceForm({ ...incomeSourceForm, endDate: event.target.value })
              }
            />
          </label>
          <label>
            Note
            <textarea
              value={incomeSourceForm.note}
              onChange={(event) =>
                setIncomeSourceForm({ ...incomeSourceForm, note: event.target.value })
              }
            />
          </label>
          <button className="primary" type="submit" disabled={busy}>
            Add source
          </button>
        </FormPanel>
      </section>

      <section className="grid">
        <DataPanel
          title={`Expenses (${dashboard.expenseMeta.pageCount}/${dashboard.expenseMeta.totalCount})`}
          items={dashboard.expenses}
          emptyText="No expenses yet."
          onDelete={(item) => handleDelete(`/api/v1/expenses/${item.id}`, "Expense archived.")}
          renderItem={(item) => (
            <>
              <strong>{formatMoney(item.amount)}</strong>
              <span>{item.date}</span>
              <span>{item.note || "No note"}</span>
            </>
          )}
        />

        <DataPanel
          title={`Incomes (${dashboard.incomeMeta.pageCount}/${dashboard.incomeMeta.totalCount})`}
          items={dashboard.incomes}
          emptyText="No incomes yet."
          onDelete={(item) => handleDelete(`/api/v1/incomes/${item.id}`, "Income archived.")}
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
          onDelete={(item) =>
            handleDelete(`/api/v1/income-sources/${item.id}`, "Income source archived.")
          }
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

function DataPanel({ title, items, emptyText, renderItem, onDelete }) {
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
              <button type="button" className="ghost" onClick={() => onDelete(item)}>
                Archive
              </button>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

function MetricCard({ title, value, negative = false }) {
  return (
    <article className={`metric ${negative ? "metric-negative" : ""}`}>
      <span>{title}</span>
      <strong>{formatMoney(value)}</strong>
    </article>
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

export default App;
