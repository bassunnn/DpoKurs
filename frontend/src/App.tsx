import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  Apple,
  Brain,
  Clock,
  Droplets,
  Dumbbell,
  Footprints,
  HeartPulse,
  Leaf,
  Loader2,
  Moon,
  Plus,
  RefreshCw,
  Sun,
  Trash2,
} from 'lucide-react'
import './App.css'

type HabitStats = {
  completedInRange: number
  currentStreak: number
  bestStreak: number
  completionRate: number
}

type Habit = {
  id: string
  title: string
  category: string
  color: string
  icon: string
  targetPerWeek: number
  reminderTime: string | null
  createdAtUtc: string
  completedDates: string[]
  stats: HabitStats
}

type Dashboard = {
  activeHabits: number
  completedToday: number
  completedThisWeek: number
  currentBestStreak: number
  averageCompletionRate: number
  lastSevenDays: Array<{
    date: string
    completed: number
    planned: number
  }>
}

type HabitForm = {
  title: string
  category: string
  color: string
  icon: string
  targetPerWeek: number
  reminderTime: string
}

const apiBase = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

const iconMap = {
  apple: Apple,
  brain: Brain,
  droplets: Droplets,
  dumbbell: Dumbbell,
  footprints: Footprints,
  heart: HeartPulse,
  leaf: Leaf,
  moon: Moon,
  sun: Sun,
}

const iconOptions = [
  { value: 'leaf', label: 'Баланс' },
  { value: 'droplets', label: 'Вода' },
  { value: 'footprints', label: 'Шаги' },
  { value: 'dumbbell', label: 'Спорт' },
  { value: 'moon', label: 'Сон' },
  { value: 'apple', label: 'Еда' },
  { value: 'heart', label: 'Пульс' },
  { value: 'brain', label: 'Фокус' },
  { value: 'sun', label: 'Утро' },
] as const

const initialForm: HabitForm = {
  title: '',
  category: 'Активность',
  color: '#2f7d6d',
  icon: 'leaf',
  targetPerWeek: 5,
  reminderTime: '',
}

function App() {
  const [habits, setHabits] = useState<Habit[]>([])
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [form, setForm] = useState<HabitForm>(initialForm)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const weekDays = useMemo(() => getLastSevenDays(), [])
  const today = weekDays[weekDays.length - 1].key

  async function loadData() {
    setError(null)
    try {
      const [habitsResponse, dashboardResponse] = await Promise.all([
        fetch(`${apiBase}/api/habits`),
        fetch(`${apiBase}/api/dashboard`),
      ])

      if (!habitsResponse.ok || !dashboardResponse.ok) {
        throw new Error('API вернул ошибку')
      }

      setHabits(await habitsResponse.json())
      setDashboard(await dashboardResponse.json())
    } catch {
      setError('Не удалось подключиться к API. Проверьте, что ASP.NET backend и PostgreSQL запущены.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  async function createHabit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSaving(true)
    setError(null)

    try {
      const response = await fetch(`${apiBase}/api/habits`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...form,
          title: form.title.trim(),
          category: form.category.trim(),
          reminderTime: form.reminderTime || null,
        }),
      })

      if (!response.ok) {
        throw new Error('Не удалось сохранить привычку')
      }

      setForm(initialForm)
      await loadData()
    } catch {
      setError('Проверьте данные привычки и попробуйте снова.')
    } finally {
      setIsSaving(false)
    }
  }

  async function toggleCheckIn(habitId: string, date: string) {
    setError(null)
    try {
      const response = await fetch(`${apiBase}/api/habits/${habitId}/checkins/toggle`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ date, note: null }),
      })

      if (!response.ok) {
        throw new Error('Не удалось обновить отметку')
      }

      await loadData()
    } catch {
      setError('Отметка не сохранилась. Проверьте соединение с API.')
    }
  }

  async function deleteHabit(habitId: string) {
    setError(null)
    try {
      const response = await fetch(`${apiBase}/api/habits/${habitId}`, { method: 'DELETE' })
      if (!response.ok) {
        throw new Error('Не удалось удалить привычку')
      }

      await loadData()
    } catch {
      setError('Не удалось удалить привычку.')
    }
  }

  return (
    <main className="app-shell">
      <section className="app-header" aria-label="Сводка">
        <div>
          <p className="eyebrow">Healthy Habits</p>
          <h1>Трекер привычек здорового образа жизни</h1>
        </div>
        <button className="icon-button" type="button" onClick={loadData} title="Обновить данные">
          <RefreshCw size={19} />
        </button>
      </section>

      {error && <div className="alert">{error}</div>}

      <section className="metrics-grid" aria-label="Показатели">
        <Metric label="Активные привычки" value={dashboard?.activeHabits ?? 0} />
        <Metric label="Сегодня выполнено" value={dashboard?.completedToday ?? 0} />
        <Metric label="За неделю" value={dashboard?.completedThisWeek ?? 0} />
        <Metric label="Лучшая серия" value={`${dashboard?.currentBestStreak ?? 0} дн.`} />
      </section>

      <section className="progress-band" aria-label="Прогресс за 7 дней">
        <div className="band-head">
          <div>
            <h2>Последние 7 дней</h2>
            <p>{dashboard?.averageCompletionRate ?? 0}% среднего выполнения</p>
          </div>
        </div>
        <div className="week-bars">
          {(dashboard?.lastSevenDays ?? weekDays.map((day) => ({ date: day.key, completed: 0, planned: habits.length }))).map(
            (day) => {
              const ratio = day.planned === 0 ? 0 : Math.round((day.completed / day.planned) * 100)
              return (
                <div className="week-bar" key={day.date}>
                  <div className="bar-track">
                    <span style={{ height: `${ratio}%` }} />
                  </div>
                  <strong>{day.completed}/{day.planned}</strong>
                  <small>{formatShortDate(day.date)}</small>
                </div>
              )
            },
          )}
        </div>
      </section>

      <div className="content-grid">
        <section className="habits-panel" aria-label="Привычки">
          <div className="panel-head">
            <div>
              <h2>Привычки</h2>
              <p>{formatLongDate(today)}</p>
            </div>
          </div>

          {isLoading ? (
            <div className="loading-state">
              <Loader2 className="spin" size={24} />
              Загрузка
            </div>
          ) : (
            <div className="habit-list">
              {habits.map((habit) => (
                <HabitCard
                  habit={habit}
                  key={habit.id}
                  weekDays={weekDays}
                  onToggle={toggleCheckIn}
                  onDelete={deleteHabit}
                />
              ))}
            </div>
          )}
        </section>

        <section className="form-panel" aria-label="Новая привычка">
          <h2>Новая привычка</h2>
          <form onSubmit={createHabit}>
            <label>
              Название
              <input
                required
                maxLength={120}
                value={form.title}
                onChange={(event) => setForm({ ...form, title: event.target.value })}
                placeholder="Например: зарядка утром"
              />
            </label>

            <label>
              Категория
              <input
                required
                maxLength={60}
                value={form.category}
                onChange={(event) => setForm({ ...form, category: event.target.value })}
              />
            </label>

            <div className="form-row">
              <label>
                Раз в неделю
                <input
                  min={1}
                  max={7}
                  type="number"
                  value={form.targetPerWeek}
                  onChange={(event) => setForm({ ...form, targetPerWeek: Number(event.target.value) })}
                />
              </label>
              <label>
                Время
                <input
                  type="time"
                  value={form.reminderTime}
                  onChange={(event) => setForm({ ...form, reminderTime: event.target.value })}
                />
              </label>
            </div>

            <label>
              Цвет
              <input
                className="color-input"
                type="color"
                value={form.color}
                onChange={(event) => setForm({ ...form, color: event.target.value })}
              />
            </label>

            <fieldset>
              <legend>Иконка</legend>
              <div className="icon-picker">
                {iconOptions.map((option) => {
                  const Icon = iconMap[option.value]
                  return (
                    <button
                      aria-pressed={form.icon === option.value}
                      className={form.icon === option.value ? 'selected' : ''}
                      key={option.value}
                      title={option.label}
                      type="button"
                      onClick={() => setForm({ ...form, icon: option.value })}
                    >
                      <Icon size={18} />
                    </button>
                  )
                })}
              </div>
            </fieldset>

            <button className="primary-button" disabled={isSaving} type="submit">
              {isSaving ? <Loader2 className="spin" size={18} /> : <Plus size={18} />}
              Добавить
            </button>
          </form>
        </section>
      </div>
    </main>
  )
}

function HabitCard({
  habit,
  weekDays,
  onToggle,
  onDelete,
}: {
  habit: Habit
  weekDays: Array<{ key: string; label: string }>
  onToggle: (habitId: string, date: string) => void
  onDelete: (habitId: string) => void
}) {
  const Icon = iconMap[habit.icon as keyof typeof iconMap] ?? Leaf
  const completed = new Set(habit.completedDates)

  return (
    <article className="habit-card">
      <div className="habit-main">
        <div className="habit-icon" style={{ background: `${habit.color}1f`, color: habit.color }}>
          <Icon size={22} />
        </div>
        <div>
          <h3>{habit.title}</h3>
          <p>{habit.category}</p>
        </div>
        <button className="icon-button danger" type="button" onClick={() => onDelete(habit.id)} title="Удалить привычку">
          <Trash2 size={18} />
        </button>
      </div>

      <div className="habit-meta">
        <span>{habit.stats.completionRate}%</span>
        <span>{habit.stats.currentStreak} дн. серия</span>
        <span>{habit.targetPerWeek}/7 цель</span>
        {habit.reminderTime && (
          <span className="time-chip">
            <Clock size={14} />
            {habit.reminderTime}
          </span>
        )}
      </div>

      <div className="check-grid">
        {weekDays.map((day) => {
          const isDone = completed.has(day.key)
          return (
            <button
              className={isDone ? 'checked' : ''}
              key={day.key}
              onClick={() => onToggle(habit.id, day.key)}
              style={isDone ? { background: habit.color, borderColor: habit.color } : undefined}
              title={`${habit.title}: ${day.label}`}
              type="button"
            >
              <span>{day.label}</span>
            </button>
          )
        })}
      </div>
    </article>
  )
}

function Metric({ label, value }: { label: string; value: number | string }) {
  return (
    <article className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  )
}

function getLastSevenDays() {
  const today = new Date()
  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(today)
    date.setDate(today.getDate() - (6 - index))
    const key = toDateKey(date)
    return {
      key,
      label: date.toLocaleDateString('ru-RU', { weekday: 'short' }).replace('.', ''),
    }
  })
}

function toDateKey(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatShortDate(dateKey: string) {
  return new Date(`${dateKey}T00:00:00`).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit' })
}

function formatLongDate(dateKey: string) {
  return new Date(`${dateKey}T00:00:00`).toLocaleDateString('ru-RU', {
    day: 'numeric',
    month: 'long',
    weekday: 'long',
  })
}

export default App
