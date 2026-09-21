import { useEffect, useState } from 'react'

/** The current time in ms, re-read every `intervalMs` so countdowns keep moving between data refetches. */
export function useNow(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])

  return now
}
