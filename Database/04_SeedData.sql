-- =============================================
-- 04 - Seed Data (sample games)
-- =============================================

USE GamePlannerDB;
GO

INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags) VALUES
('Stardew Valley',       'Simulation',   30,  'chill'),
('Hollow Knight',        'Metroidvania', 60,  'focused'),
('It Takes Two',         'Platformer',   60,  'coop,story'),
('The Witcher 3',        'RPG',          120, 'story,focused'),
('Among Us',             'Party',        30,  'chill,coop'),
('Celeste',              'Platformer',   60,  'focused'),
('A Way Out',            'Adventure',    120, 'coop,story'),
('Animal Crossing',      'Simulation',   30,  'chill'),
('Hades',                'Roguelike',    60,  'focused'),
('Overcooked 2',         'Party',        30,  'chill,coop');
GO
