IF DB_ID(N'DynamicSurveySearchDb') IS NULL
BEGIN
    CREATE DATABASE DynamicSurveySearchDb;
END;
GO

USE DynamicSurveySearchDb;
GO

IF OBJECT_ID(N'dbo.form_type', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.form_type
    (
        FormTypeId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_form_type PRIMARY KEY,
        Name           nvarchar(100) NOT NULL,
        Description    nvarchar(500) NULL,
        CONSTRAINT UQ_form_type_Name UNIQUE (Name)
    );
END;
GO

IF OBJECT_ID(N'dbo.survey_form', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.survey_form
    (
        SurveyFormId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_survey_form PRIMARY KEY,
        FormTypeId     int NOT NULL,
        Code           nvarchar(50) NOT NULL,
        Name           nvarchar(200) NOT NULL,
        Description    nvarchar(1000) NULL,
        CONSTRAINT UQ_survey_form_Code UNIQUE (Code),
        CONSTRAINT FK_survey_form_form_type FOREIGN KEY (FormTypeId)
            REFERENCES dbo.form_type(FormTypeId)
    );
END;
GO

IF OBJECT_ID(N'dbo.survey_form_version', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.survey_form_version
    (
        SurveyFormVersionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_survey_form_version PRIMARY KEY,
        SurveyFormId        int NOT NULL,
        VersionNumber       int NOT NULL,
        Status              nvarchar(20) NOT NULL,
        CreatedAtUtc        datetime2 NOT NULL,
        PublishedAtUtc      datetime2 NULL,
        CONSTRAINT UQ_survey_form_version UNIQUE (SurveyFormId, VersionNumber),
        CONSTRAINT CK_survey_form_version_Status CHECK (Status IN ('Draft', 'Published', 'Retired')),
        CONSTRAINT FK_survey_form_version_form FOREIGN KEY (SurveyFormId)
            REFERENCES dbo.survey_form(SurveyFormId) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.question_list', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.question_list
    (
        QuestionDefinitionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_question_list PRIMARY KEY,
        SurveyFormVersionId  int NOT NULL,
        QuestionKey          nvarchar(100) NOT NULL,
        QuestionText         nvarchar(1000) NOT NULL,
        HelpText             nvarchar(500) NULL,
        QuestionType         nvarchar(30) NOT NULL,
        IsRequired           bit NOT NULL,
        DisplayOrder         int NOT NULL,
        CONSTRAINT UQ_question_list_Key UNIQUE (SurveyFormVersionId, QuestionKey),
        CONSTRAINT CK_question_list_Type CHECK
        (
            QuestionType IN
            ('ShortText', 'LongText', 'SingleChoice', 'MultipleChoice', 'Date', 'Number', 'Boolean')
        ),
        CONSTRAINT FK_question_list_version FOREIGN KEY (SurveyFormVersionId)
            REFERENCES dbo.survey_form_version(SurveyFormVersionId) ON DELETE CASCADE
    );

    CREATE INDEX IX_question_list_Version_Order
        ON dbo.question_list(SurveyFormVersionId, DisplayOrder);
END;
GO

IF OBJECT_ID(N'dbo.question_option', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.question_option
    (
        QuestionOptionId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_question_option PRIMARY KEY,
        QuestionDefinitionId int NOT NULL,
        OptionValue          nvarchar(100) NOT NULL,
        OptionText           nvarchar(300) NOT NULL,
        DisplayOrder         int NOT NULL,
        CONSTRAINT UQ_question_option_Value UNIQUE (QuestionDefinitionId, OptionValue),
        CONSTRAINT FK_question_option_question FOREIGN KEY (QuestionDefinitionId)
            REFERENCES dbo.question_list(QuestionDefinitionId) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.survey_result', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.survey_result
    (
        SurveyResultId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_survey_result PRIMARY KEY,
        SurveyFormVersionId int NOT NULL,
        PublicId            uniqueidentifier NOT NULL,
        SubmittedAtUtc      datetime2 NOT NULL,
        SubmittedBy         nvarchar(200) NULL,
        CONSTRAINT UQ_survey_result_PublicId UNIQUE (PublicId),
        CONSTRAINT FK_survey_result_version FOREIGN KEY (SurveyFormVersionId)
            REFERENCES dbo.survey_form_version(SurveyFormVersionId)
    );

    CREATE INDEX IX_survey_result_Version_Date
        ON dbo.survey_result(SurveyFormVersionId, SubmittedAtUtc);
END;
GO

IF OBJECT_ID(N'dbo.survey_answer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.survey_answer
    (
        SurveyAnswerId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_survey_answer PRIMARY KEY,
        SurveyResultId      bigint NOT NULL,
        QuestionDefinitionId int NOT NULL,
        TextValue           nvarchar(2000) NULL,
        NumberValue         decimal(18,4) NULL,
        DateValue           datetime2 NULL,
        BooleanValue        bit NULL,
        CONSTRAINT UQ_survey_answer_Result_Question UNIQUE (SurveyResultId, QuestionDefinitionId),
        CONSTRAINT FK_survey_answer_result FOREIGN KEY (SurveyResultId)
            REFERENCES dbo.survey_result(SurveyResultId) ON DELETE CASCADE,
        CONSTRAINT FK_survey_answer_question FOREIGN KEY (QuestionDefinitionId)
            REFERENCES dbo.question_list(QuestionDefinitionId)
    );

    CREATE INDEX IX_survey_answer_Question_Date_Result
        ON dbo.survey_answer(QuestionDefinitionId, DateValue, SurveyResultId);
    CREATE INDEX IX_survey_answer_Question_Number_Result
        ON dbo.survey_answer(QuestionDefinitionId, NumberValue, SurveyResultId);
    CREATE INDEX IX_survey_answer_Question_Boolean_Result
        ON dbo.survey_answer(QuestionDefinitionId, BooleanValue, SurveyResultId);
END;
GO

IF OBJECT_ID(N'dbo.survey_answer_option', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.survey_answer_option
    (
        SurveyAnswerOptionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_survey_answer_option PRIMARY KEY,
        SurveyAnswerId       bigint NOT NULL,
        QuestionOptionId     int NOT NULL,
        CONSTRAINT UQ_survey_answer_option UNIQUE (SurveyAnswerId, QuestionOptionId),
        CONSTRAINT FK_survey_answer_option_answer FOREIGN KEY (SurveyAnswerId)
            REFERENCES dbo.survey_answer(SurveyAnswerId) ON DELETE CASCADE,
        CONSTRAINT FK_survey_answer_option_option FOREIGN KEY (QuestionOptionId)
            REFERENCES dbo.question_option(QuestionOptionId)
    );

    CREATE INDEX IX_survey_answer_option_Option_Answer
        ON dbo.survey_answer_option(QuestionOptionId, SurveyAnswerId);
END;
GO

IF OBJECT_ID(N'dbo.search_definition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.search_definition
    (
        SearchDefinitionId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_search_definition PRIMARY KEY,
        SurveyFormVersionId int NOT NULL,
        Name                 nvarchar(200) NOT NULL,
        IsActive             bit NOT NULL,
        CreatedAtUtc         datetime2 NOT NULL,
        CONSTRAINT UQ_search_definition_Name UNIQUE (SurveyFormVersionId, Name),
        CONSTRAINT FK_search_definition_version FOREIGN KEY (SurveyFormVersionId)
            REFERENCES dbo.survey_form_version(SurveyFormVersionId) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.search_field_definition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.search_field_definition
    (
        SearchFieldDefinitionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_search_field_definition PRIMARY KEY,
        SearchDefinitionId      int NOT NULL,
        QuestionDefinitionId    int NOT NULL,
        DisplayLabel            nvarchar(200) NOT NULL,
        DefaultOperator         nvarchar(30) NOT NULL,
        DisplayOrder            int NOT NULL,
        CONSTRAINT UQ_search_field_definition UNIQUE (SearchDefinitionId, QuestionDefinitionId),
        CONSTRAINT FK_search_field_definition_search FOREIGN KEY (SearchDefinitionId)
            REFERENCES dbo.search_definition(SearchDefinitionId) ON DELETE CASCADE,
        CONSTRAINT FK_search_field_definition_question FOREIGN KEY (QuestionDefinitionId)
            REFERENCES dbo.question_list(QuestionDefinitionId)
    );
END;
GO
