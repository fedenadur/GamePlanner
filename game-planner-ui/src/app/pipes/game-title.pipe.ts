import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'gameTitle', standalone: false })
export class GameTitlePipe implements PipeTransform {
  transform(game: { name: string; releaseYear?: number | null } | null | undefined): string {
    if (!game) return '';
    return game.releaseYear ? `${game.name} (${game.releaseYear})` : game.name;
  }
}
