import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ComingSoon } from '../../../src/components/common/ComingSoon'
import { renderWithProviders } from '../test-utils'

describe('ComingSoon', () => {
  it('顯示頁面名稱、建置中標示與用途說明', () => {
    renderWithProviders(<ComingSoon titleKey="pages.hosts.title" descriptionKey="pages.hosts.description" />)

    expect(screen.getByRole('heading', { name: '主機' })).toBeInTheDocument()
    expect(screen.getByText('建置中')).toBeInTheDocument()
    expect(screen.getByText(/後端主機/)).toBeInTheDocument()
  })
})
