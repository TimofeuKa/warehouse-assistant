<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'

type OperationType = 'income' | 'expense' | 'transfer'

type CatalogItem = {
  id: number
  name: string
}

type CatalogsResponse = {
  warehouses: CatalogItem[]
  nomenclatures: CatalogItem[]
}

type MovementItem = {
  nomenclatureId: number
  nomenclatureName: string
  quantity: number
}

type Movement = {
  id: number
  occurredAt: string
  fromWarehouseId: number | null
  fromWarehouseName: string | null
  toWarehouseId: number | null
  toWarehouseName: string | null
  items: MovementItem[]
}

type StockRow = {
  nomenclatureId: number
  nomenclatureName: string
  quantity: number
}

type MovementLineDraft = {
  nomenclatureId: string
  quantity: number
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5052'

const catalogs = reactive<CatalogsResponse>({
  warehouses: [],
  nomenclatures: [],
})

const movements = ref<Movement[]>([])
const stockRows = ref<StockRow[]>([])
const isLoading = ref(false)
const hasLoaded = ref(false)
const error = ref('')
const success = ref('')

const form = reactive({
  operationType: 'income' as OperationType,
  occurredAt: toDateTimeLocal(new Date()),
  fromWarehouseId: '',
  toWarehouseId: '',
  items: [{ nomenclatureId: '', quantity: 1 }] as MovementLineDraft[],
})

const stockQuery = reactive({
  warehouseId: '',
  at: toDateTimeLocal(new Date()),
})

const duplicateNomenclature = computed(() => {
  const ids = form.items.map((x) => x.nomenclatureId).filter(Boolean)
  return new Set(ids).size !== ids.length
})

const sameWarehouse = computed(() => {
  return form.operationType === 'transfer' && Boolean(form.fromWarehouseId && form.fromWarehouseId === form.toWarehouseId)
})

const fromWarehouseForRequest = computed(() => {
  return form.operationType === 'income' ? null : parseNullableInt(form.fromWarehouseId)
})

const toWarehouseForRequest = computed(() => {
  return form.operationType === 'expense' ? null : parseNullableInt(form.toWarehouseId)
})

const canSubmitMovement = computed(() => {
  const hasRequiredWarehouses =
    form.operationType === 'income'
      ? Boolean(form.toWarehouseId)
      : form.operationType === 'expense'
        ? Boolean(form.fromWarehouseId)
        : Boolean(form.fromWarehouseId && form.toWarehouseId && !sameWarehouse.value)

  return Boolean(
    hasRequiredWarehouses &&
      form.items.length > 0 &&
      form.items.every((x) => x.nomenclatureId && Number.isInteger(Number(x.quantity)) && Number(x.quantity) > 0) &&
      !duplicateNomenclature.value,
  )
})

watch(
  () => form.operationType,
  () => {
    if (form.operationType === 'income') {
      form.fromWarehouseId = ''
    }

    if (form.operationType === 'expense') {
      form.toWarehouseId = ''
    }
  },
)

onMounted(async () => {
  await loadInitialData()
})

async function loadInitialData() {
  await loadCatalogs()
  await loadMovements()

  if (!stockQuery.warehouseId && catalogs.warehouses.length > 0) {
    stockQuery.warehouseId = String(catalogs.warehouses[0].id)
  }

  await loadStock()
  hasLoaded.value = true
}

async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  })

  if (!response.ok) {
    let message = `Ошибка API: ${response.status}`

    try {
      const body = await response.json()
      message = body.error ?? message
    } catch {
      // Оставляем статус, если тело ответа не JSON.
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

async function withLoading(action: () => Promise<void>) {
  isLoading.value = true
  error.value = ''

  try {
    await action()
  } catch (e) {
    success.value = ''
    error.value = e instanceof Error ? e.message : 'Неизвестная ошибка'
  } finally {
    isLoading.value = false
  }
}

async function loadCatalogs() {
  await withLoading(async () => {
    const data = await api<CatalogsResponse>('/api/catalogs')
    catalogs.warehouses = data.warehouses
    catalogs.nomenclatures = data.nomenclatures
  })
}

async function loadMovements() {
  await withLoading(async () => {
    movements.value = await api<Movement[]>('/api/movements')
  })
}

async function createMovement() {
  if (!canSubmitMovement.value) {
    error.value = 'Проверьте тип операции, склады, номенклатуры и количество.'
    return
  }

  await withLoading(async () => {
    await api<{ id: number }>('/api/movements', {
      method: 'POST',
      body: JSON.stringify({
        occurredAt: toIso(form.occurredAt),
        fromWarehouseId: fromWarehouseForRequest.value,
        toWarehouseId: toWarehouseForRequest.value,
        items: form.items.map((x) => ({
          nomenclatureId: Number(x.nomenclatureId),
          quantity: Number(x.quantity),
        })),
      }),
    })

    success.value = 'Движение создано.'
    resetMovementForm()
    await loadMovements()
    await loadStock()
  })
}

async function deleteMovement(id: number) {
  const confirmed = window.confirm('Удалить движение? Остатки будут пересчитаны.')
  if (!confirmed) {
    return
  }

  await withLoading(async () => {
    await api<void>(`/api/movements/${id}`, { method: 'DELETE' })
    success.value = 'Движение удалено.'
    await loadMovements()
    await loadStock()
  })
}

async function loadStock() {
  if (!stockQuery.warehouseId) {
    stockRows.value = []
    return
  }

  await withLoading(async () => {
    const params = new URLSearchParams({
      warehouseId: stockQuery.warehouseId,
      at: toIso(stockQuery.at),
    })

    stockRows.value = await api<StockRow[]>(`/api/stocks?${params}`)
  })
}

function addLine() {
  form.items.push({ nomenclatureId: '', quantity: 1 })
}

function removeLine(index: number) {
  if (form.items.length === 1) {
    form.items[0] = { nomenclatureId: '', quantity: 1 }
    return
  }

  form.items.splice(index, 1)
}

function resetMovementForm() {
  form.operationType = 'income'
  form.occurredAt = toDateTimeLocal(new Date())
  form.fromWarehouseId = ''
  form.toWarehouseId = ''
  form.items = [{ nomenclatureId: '', quantity: 1 }]
}

function movementType(movement: Movement) {
  if (!movement.fromWarehouseId) return 'Приход'
  if (!movement.toWarehouseId) return 'Расход'
  return 'Перемещение'
}

function movementDirection(movement: Movement) {
  return `${movement.fromWarehouseName ?? 'Извне'} -> ${movement.toWarehouseName ?? 'Вне компании'}`
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function formatQuantity(value: number) {
  return new Intl.NumberFormat('ru-RU', {
    maximumFractionDigits: 0,
  }).format(value)
}

function parseNullableInt(value: string) {
  return value ? Number(value) : null
}

function toIso(value: string) {
  return new Date(value).toISOString()
}

function toDateTimeLocal(value: Date) {
  const offset = value.getTimezoneOffset()
  const local = new Date(value.getTime() - offset * 60_000)
  return local.toISOString().slice(0, 16)
}
</script>

<template>
  <main class="app-shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">Складской учет</p>
        <h1>Помощник кладовщика</h1>
      </div>
      <div class="status" :class="{ busy: isLoading }">
        {{ isLoading ? 'Синхронизация' : 'API подключен' }}
      </div>
    </header>

    <section v-if="error || success" class="messages">
      <p v-if="error" class="message error">{{ error }}</p>
      <p v-if="success" class="message success">{{ success }}</p>
    </section>

    <section class="work-grid">
      <form class="panel movement-form" @submit.prevent="createMovement">
        <div class="panel-header">
          <div>
            <h2>Новое движение</h2>
            <p>Выберите тип операции и заполните список ТМЦ</p>
          </div>
          <button class="secondary" type="button" @click="resetMovementForm">Сбросить</button>
        </div>

        <div class="segment-control" aria-label="Тип операции">
          <button
            type="button"
            :class="{ active: form.operationType === 'income' }"
            @click="form.operationType = 'income'"
          >
            Приход
          </button>
          <button
            type="button"
            :class="{ active: form.operationType === 'expense' }"
            @click="form.operationType = 'expense'"
          >
            Расход
          </button>
          <button
            type="button"
            :class="{ active: form.operationType === 'transfer' }"
            @click="form.operationType = 'transfer'"
          >
            Перемещение
          </button>
        </div>

        <label>
          Время операции
          <input v-model="form.occurredAt" type="datetime-local" required />
        </label>

        <div class="field-grid">
          <label v-if="form.operationType !== 'income'">
            Откуда
            <select v-model="form.fromWarehouseId" required>
              <option value="" disabled>Выберите склад</option>
              <option v-for="warehouse in catalogs.warehouses" :key="warehouse.id" :value="warehouse.id">
                {{ warehouse.name }}
              </option>
            </select>
          </label>

          <label v-if="form.operationType !== 'expense'">
            Куда
            <select v-model="form.toWarehouseId" required>
              <option value="" disabled>Выберите склад</option>
              <option v-for="warehouse in catalogs.warehouses" :key="warehouse.id" :value="warehouse.id">
                {{ warehouse.name }}
              </option>
            </select>
          </label>
        </div>

        <div class="lines-head">
          <span>ТМЦ в движении</span>
          <button class="secondary" type="button" @click="addLine">Добавить строку</button>
        </div>

        <div class="movement-lines">
          <div v-for="(line, index) in form.items" :key="index" class="movement-line">
            <select v-model="line.nomenclatureId" required>
              <option value="" disabled>Номенклатура</option>
              <option v-for="item in catalogs.nomenclatures" :key="item.id" :value="item.id">
                {{ item.name }}
              </option>
            </select>

            <input v-model.number="line.quantity" type="number" min="1" step="1" required />

            <button class="icon-button" type="button" title="Удалить строку" @click="removeLine(index)">
              x
            </button>
          </div>
        </div>

        <p v-if="sameWarehouse" class="hint warning">Склады отправления и получения не должны совпадать.</p>
        <p v-if="duplicateNomenclature" class="hint warning">Номенклатура не должна повторяться в одном движении.</p>

        <button class="primary" type="submit" :disabled="!canSubmitMovement || isLoading">
          Создать движение
        </button>
      </form>

      <section class="panel stock-panel">
        <div class="panel-header">
          <div>
            <h2>Остатки</h2>
            <p>Состояние склада на выбранное время</p>
          </div>
          <button class="secondary" type="button" @click="loadStock">Обновить</button>
        </div>

        <div class="field-grid">
          <label>
            Склад
            <select v-model="stockQuery.warehouseId" @change="loadStock">
              <option value="" disabled>Выберите склад</option>
              <option v-for="warehouse in catalogs.warehouses" :key="warehouse.id" :value="warehouse.id">
                {{ warehouse.name }}
              </option>
            </select>
          </label>

          <label>
            На время
            <input v-model="stockQuery.at" type="datetime-local" @change="loadStock" />
          </label>
        </div>

        <div v-if="isLoading && !hasLoaded" class="empty">Загрузка остатков...</div>
        <div v-else-if="stockRows.length === 0" class="empty">Выберите склад для просмотра остатков.</div>
        <div v-else class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Номенклатура</th>
                <th class="numeric">Остаток</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in stockRows" :key="row.nomenclatureId">
                <td>{{ row.nomenclatureName }}</td>
                <td class="numeric" :class="{ negative: row.quantity < 0 }">
                  {{ formatQuantity(row.quantity) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>

    <section class="panel journal-panel">
      <div class="panel-header">
        <div>
          <h2>Журнал движений</h2>
          <p>Все приходы, расходы и перемещения</p>
        </div>
        <button class="secondary" type="button" @click="loadMovements">Обновить</button>
      </div>

      <div v-if="isLoading && !hasLoaded" class="empty">Загрузка движений...</div>
      <div v-else-if="movements.length === 0" class="empty">Движений пока нет.</div>

      <div v-else class="movement-list">
        <article v-for="movement in movements" :key="movement.id" class="movement-card">
          <div class="movement-main">
            <span class="badge">{{ movementType(movement) }}</span>
            <strong>{{ movementDirection(movement) }}</strong>
            <time>{{ formatDate(movement.occurredAt) }}</time>
          </div>

          <div class="movement-items">
            <span v-for="item in movement.items" :key="item.nomenclatureId">
              {{ item.nomenclatureName }}: {{ formatQuantity(item.quantity) }}
            </span>
          </div>

          <button class="danger" type="button" @click="deleteMovement(movement.id)">Удалить</button>
        </article>
      </div>
    </section>
  </main>
</template>
